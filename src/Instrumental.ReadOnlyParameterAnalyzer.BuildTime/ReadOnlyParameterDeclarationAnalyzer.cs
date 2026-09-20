using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Instrumental.ReadOnlyParameterAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterDeclarationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Diagnostics.InvalidReadOnlyParameter.Descriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(AnalyzeAccessor,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.InitAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var syntax = (ParameterSyntax)context.Node;
        if (syntax.AttributeLists.Count == 0
            || HasExecutableBody(syntax.Parent?.Parent)
            || context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) is not IParameterSymbol parameter)
        {
            return;
        }

        ReportInvalidAttributes(context, parameter, syntax);
    }

    private static void AnalyzeAccessor(SyntaxNodeAnalysisContext context)
    {
        var syntax = (AccessorDeclarationSyntax)context.Node;
        if (syntax.AttributeLists.Count == 0
            || HasExecutableBody(syntax)
            || context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) is not { Parameters.IsEmpty: false } accessor)
        {
            return;
        }

        ReportInvalidAttributes(context, accessor.Parameters.Last(), syntax);
    }

    private static void ReportInvalidAttributes(SyntaxNodeAnalysisContext context, IParameterSymbol parameter, SyntaxNode syntax)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (ReadOnlyParameterMutationFacts.IsReadOnlyParameterAttribute(attribute)
                && attribute.ApplicationSyntaxReference is { } application
                && application.SyntaxTree == syntax.SyntaxTree
                && syntax.Span.Contains(application.Span))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidReadOnlyParameter.Descriptor,
                    application.GetSyntax(context.CancellationToken).GetLocation(),
                    parameter.Name));
            }
        }
    }

    private static bool HasExecutableBody(SyntaxNode declaration)
    {
        return declaration switch
        {
            DelegateDeclarationSyntax => false,
            ConstructorDeclarationSyntax { Initializer: not null } => true,
            BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
            LocalFunctionStatementSyntax localFunction => localFunction.Body is not null || localFunction.ExpressionBody is not null,
            AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
            IndexerDeclarationSyntax indexer => indexer.ExpressionBody is not null
                || indexer.AccessorList?.Accessors.Any(accessor => accessor.Body is not null || accessor.ExpressionBody is not null) == true,
            _ => true
        };
    }
}
