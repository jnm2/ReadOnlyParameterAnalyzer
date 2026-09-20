using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Text;

namespace Instrumental.ReadOnlyParameterAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterMutationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Diagnostics.ReadOnlyParameterMutation.Descriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeOperation,
            OperationKind.SimpleAssignment,
            OperationKind.CompoundAssignment,
            OperationKind.CoalesceAssignment,
            OperationKind.DeconstructionAssignment,
            OperationKind.Increment,
            OperationKind.Decrement,
            OperationKind.Invocation,
            OperationKind.Argument,
            OperationKind.VariableDeclarator,
            OperationKind.DynamicInvocation,
            OperationKind.DynamicObjectCreation);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IAssignmentOperation assignmentOperation:
                var isRefAssignment = assignmentOperation is ISimpleAssignmentOperation { IsRef: true };
                var operatorText = isRefAssignment ? "= ref" : ((AssignmentExpressionSyntax)assignmentOperation.Syntax).OperatorToken.ValueText;
                AnalyzeMutationTarget(
                    context,
                    assignmentOperation.Target,
                    $"'{operatorText}' assignment",
                    isRefAssignment,
                    inlineTarget: null);

                if (isRefAssignment && IsWritableReference(assignmentOperation.Target))
                    AnalyzeRefTaking(context, assignmentOperation.Value);
                break;
            case IIncrementOrDecrementOperation incrementOrDecrementOperation:
                AnalyzeMutationTarget(
                    context,
                    incrementOrDecrementOperation.Target,
                    incrementOrDecrementOperation.Kind == OperationKind.Increment ? "'++' assignment" : "'--' assignment",
                    isRefAssignment: false,
                    inlineTarget: null);
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argumentOperation:
                AnalyzeRefTaking(context, argumentOperation.Value);
                break;
            case IVariableDeclaratorOperation { Symbol.RefKind: RefKind.Ref, Initializer.Value: { } value }:
                AnalyzeRefTaking(context, value);
                break;
            case IDynamicInvocationOperation dynamicInvocationOperation:
                AnalyzeDynamicArguments(context, dynamicInvocationOperation.Arguments);
                break;
            case IDynamicObjectCreationOperation dynamicObjectCreationOperation:
                AnalyzeDynamicArguments(context, dynamicObjectCreationOperation.Arguments);
                break;
            case IInvocationOperation { Instance.Type.IsReferenceType: false, TargetMethod.IsReadOnly: false } invocationOperation:
                if (IsReadOnlyParameterReference(invocationOperation.Instance, out var diagnosticCreator)
                    && diagnosticCreator.ParameterReference.Parameter.RefKind == RefKind.None)
                {
                    context.ReportDiagnostic(diagnosticCreator.Create(
                        mutatedBy: $"invoking a non-readonly struct method '{invocationOperation.TargetMethod.Name}'",
                        isMissingDefensiveCopy: true));
                }
                break;
        }
    }

    private static void AnalyzeRefTaking(OperationAnalysisContext context, IOperation target)
    {
        AnalyzeMutationTarget(context, target, "taking a writable reference", isRefAssignment: false, inlineTarget: null);
    }

    private static bool IsWritableReference(IOperation operation)
    {
        return operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local.RefKind == RefKind.Ref,
            IParameterReferenceOperation parameterReference => parameterReference.Parameter.RefKind is RefKind.Ref or RefKind.Out,
            IFieldReferenceOperation fieldReference => fieldReference.Field.RefKind == RefKind.Ref,
            _ => false
        };
    }

    private static void AnalyzeDynamicArguments(OperationAnalysisContext context, ImmutableArray<IOperation> arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument.Syntax.Parent is ArgumentSyntax syntax
                && syntax.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword)
            {
                AnalyzeRefTaking(context, argument);
            }
        }
    }

    private static void AnalyzeMutationTarget(OperationAnalysisContext context, IOperation target, string mutationDescription, bool isRefAssignment, IOperation inlineTarget)
    {
        if (target is ITupleOperation tupleOperation)
        {
            foreach (var element in tupleOperation.Elements)
                AnalyzeMutationTarget(context, element, mutationDescription, isRefAssignment, inlineTarget: null);
        }
        else if (target is IConditionalOperation { IsRef: true } conditionalOperation)
        {
            AnalyzeMutationTarget(context, conditionalOperation.WhenTrue, mutationDescription, isRefAssignment, inlineTarget);
            AnalyzeMutationTarget(context, conditionalOperation.WhenFalse, mutationDescription, isRefAssignment, inlineTarget);
        }
        else if (target is IFieldReferenceOperation { Instance.Type.IsValueType: true } fieldReference)
        {
            if (fieldReference.Field.RefKind == RefKind.None || isRefAssignment)
                AnalyzeMutationTarget(context, fieldReference.Instance, mutationDescription, isRefAssignment: false, inlineTarget: inlineTarget ?? target);
        }
        else if (target is IInlineArrayAccessOperation inlineArrayAccess)
        {
            AnalyzeMutationTarget(context, inlineArrayAccess.Instance, mutationDescription, isRefAssignment: false, inlineTarget: inlineTarget ?? target);
        }
        else if (IsReadOnlyParameterReference(target, out var diagnosticCreator)
            && (isRefAssignment || diagnosticCreator.ParameterReference.Parameter.RefKind == RefKind.None))
        {
            var mutatedBy = new StringBuilder(mutationDescription);

            if (inlineTarget is not null)
            {
                mutatedBy.Append(" to '").Append(inlineTarget.Syntax).Append("' which is stored inline within '")
                    .Append(diagnosticCreator.ParameterReference.Parameter.Name).Append("'");
            }

            context.ReportDiagnostic(diagnosticCreator.Create(
                mutatedBy.ToString(),
                isMissingDefensiveCopy: false));
        }
    }

    private sealed record ReadOnlyParameterMutationDiagnosticCreator(
        IParameterReferenceOperation ParameterReference,
        SyntaxReference ApplicationSyntaxReference)
    {
        public Diagnostic Create(string mutatedBy, bool isMissingDefensiveCopy)
        {
            if (ApplicationSyntaxReference is null)
                throw new NotImplementedException("TODO: cover defaults (editorconfig or csproj)");

            var configuredAsReadonlyVia = $"[{ApplicationSyntaxReference.GetSyntax()}] on the parameter declaration";

            var properties = ImmutableDictionary.CreateBuilder<string, string>();
            properties.Add(Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName, ParameterReference.Parameter.Name);
            if (isMissingDefensiveCopy)
                properties.Add(Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix, null);

            return Diagnostic.Create(
                Diagnostics.ReadOnlyParameterMutation.Descriptor,
                ParameterReference.Syntax.GetLocation(),
                properties.ToImmutable(),
                messageArgs: [ParameterReference.Parameter.Name, configuredAsReadonlyVia, mutatedBy]);
        }
    }

    private static bool IsReadOnlyParameterReference(IOperation operation, out ReadOnlyParameterMutationDiagnosticCreator diagnosticCreator)
    {
        if (operation is IParameterReferenceOperation parameterReference
            && IsReadOnlyParameter(parameterReference.Parameter, out var applicationSyntaxReference))
        {
            diagnosticCreator = new(parameterReference, applicationSyntaxReference);
            return true;
        }

        diagnosticCreator = default;
        return false;
    }

    private static bool IsReadOnlyParameter(IParameterSymbol symbol, out SyntaxReference applicationSyntaxReference)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (ReadOnlyParameterMutationFacts.IsReadOnlyParameterAttribute(attribute)
                && (attribute.ConstructorArguments.IsEmpty || attribute.ConstructorArguments[0].Value is false))
            {
                applicationSyntaxReference = attribute.ApplicationSyntaxReference;
                return true;
            }
        }

        applicationSyntaxReference = null;
        return false;
    }
}
