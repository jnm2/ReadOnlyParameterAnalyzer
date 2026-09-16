using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

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
            OperationKind.Invocation);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IAssignmentOperation assignmentOperation:
                if (IsReadOnlyParameterReference(assignmentOperation.Target, out var diagnosticCreator))
                {
                    var operatorText = ((AssignmentExpressionSyntax)assignmentOperation.Syntax).OperatorToken.ValueText;

                    context.ReportDiagnostic(diagnosticCreator.Create(mutatedBy: $"'{operatorText}' assignment"));
                }
                break;
            case IInvocationOperation { Instance.Type.IsReferenceType: false, TargetMethod.IsReadOnly: false } invocationOperation:
                if (IsReadOnlyParameterReference(invocationOperation.Instance, out diagnosticCreator))
                {
                    context.ReportDiagnostic(diagnosticCreator.Create(mutatedBy: $"invoking a non-readonly struct method '{invocationOperation.TargetMethod.Name}'"));
                }
                break;
        }
    }

    private readonly struct ReadOnlyParameterMutationDiagnosticCreator(
        IParameterReferenceOperation parameterReference,
        SyntaxReference applicationSyntaxReference)
    {
        public Diagnostic Create(string mutatedBy)
        {
            if (applicationSyntaxReference is null)
                throw new NotImplementedException("TODO: cover defaults (editorconfig or csproj)");

            var configuredAsReadonlyVia = $"[{applicationSyntaxReference.GetSyntax()}] on the parameter declaration";

            var properties = ImmutableDictionary.CreateBuilder<string, string>();
            properties.Add(Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName, parameterReference.Parameter.Name);
            // TODO: conditional
            properties.Add(Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix, null);

            return Diagnostic.Create(
                Diagnostics.ReadOnlyParameterMutation.Descriptor,
                parameterReference.Syntax.GetLocation(),
                properties.ToImmutable(),
                messageArgs: [parameterReference.Parameter.Name, configuredAsReadonlyVia, mutatedBy]);
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
            if (IsReadOnlyParameterAttribute(attribute)
                && (attribute.ConstructorArguments.IsEmpty || attribute.ConstructorArguments[0].Value is false))
            {
                applicationSyntaxReference = attribute.ApplicationSyntaxReference;
                return true;
            }
        }

        applicationSyntaxReference = null;
        return false;
    }

    public static bool IsReadOnlyParameterAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass is
        {
            Name: "ReadOnlyAttribute",
            ContainingSymbol: INamespaceSymbol
            {
                Name: "Annotations",
                ContainingNamespace:
                {
                    Name: "Instrumental",
                    ContainingNamespace.IsGlobalNamespace: true
                }
            }
        };
    }
}
