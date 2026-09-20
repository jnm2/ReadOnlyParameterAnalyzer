using Microsoft.CodeAnalysis;
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
            OperationKind.Invocation);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IAssignmentOperation assignmentOperation:
                var isRefAssignment = assignmentOperation is ISimpleAssignmentOperation { IsRef: true };
                AnalyzeAssignmentTarget(
                    context,
                    assignmentOperation.Target,
                    isRefAssignment ? "= ref" : ((AssignmentExpressionSyntax)assignmentOperation.Syntax).OperatorToken.ValueText,
                    isRefAssignment,
                    inlineTarget: null);
                break;
            case IIncrementOrDecrementOperation incrementOrDecrementOperation:
                AnalyzeAssignmentTarget(
                    context,
                    incrementOrDecrementOperation.Target,
                    incrementOrDecrementOperation.Kind == OperationKind.Increment ? "++" : "--",
                    isRefAssignment: false,
                    inlineTarget: null);
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

    private static void AnalyzeAssignmentTarget(OperationAnalysisContext context, IOperation target, string operatorText, bool isRefAssignment, IOperation inlineTarget)
    {
        if (target is ITupleOperation tupleOperation)
        {
            foreach (var element in tupleOperation.Elements)
                AnalyzeAssignmentTarget(context, element, operatorText, isRefAssignment, inlineTarget: null);
        }
        else if (target is IConditionalOperation { IsRef: true } conditionalOperation)
        {
            AnalyzeAssignmentTarget(context, conditionalOperation.WhenTrue, operatorText, isRefAssignment, inlineTarget);
            AnalyzeAssignmentTarget(context, conditionalOperation.WhenFalse, operatorText, isRefAssignment, inlineTarget);
        }
        else if (target is IFieldReferenceOperation { Instance.Type.IsValueType: true } fieldReference)
        {
            if (fieldReference.Field.RefKind == RefKind.None || isRefAssignment)
                AnalyzeAssignmentTarget(context, fieldReference.Instance, operatorText, isRefAssignment: false, inlineTarget: inlineTarget ?? target);
        }
        else if (target is IInlineArrayAccessOperation inlineArrayAccess)
        {
            AnalyzeAssignmentTarget(context, inlineArrayAccess.Instance, operatorText, isRefAssignment: false, inlineTarget: inlineTarget ?? target);
        }
        else if (IsReadOnlyParameterReference(target, out var diagnosticCreator)
            && (isRefAssignment || diagnosticCreator.ParameterReference.Parameter.RefKind == RefKind.None))
        {
            var mutatedBy = new StringBuilder();
            mutatedBy.Append("'").Append(operatorText).Append("' assignment");

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
