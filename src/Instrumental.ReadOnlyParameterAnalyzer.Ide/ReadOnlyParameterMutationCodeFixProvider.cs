using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;

namespace Instrumental.ReadOnlyParameterAnalyzer;

[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterMutationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [Diagnostics.ReadOnlyParameterMutation.Descriptor.Id];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Properties.ContainsKey(Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix))
            {
                // Unless https://github.com/dotnet/roslyn/issues/84307 results in ECMA deciding to codify the de-facto
                // compiler behavior, this fix will need to introduce a temp variable instead.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Add an explicit cast so that mutations do not affect '{diagnostic.Properties[Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName]}'",
                        createChangedDocument: cancellationToken => ApplyDefensiveCopyFixAsync(context.Document, diagnostic, cancellationToken),
                        equivalenceKey: Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ApplyDefensiveCopyFixAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);

        var readOnlyExpression = syntaxRoot.FindNode(diagnostic.Location.SourceSpan);
        var expressionType = semanticModel.GetTypeInfo(readOnlyExpression, cancellationToken).Type;

        CastExpressionSyntax explicitCastExpression;

        if (expressionType.TypeKind == TypeKind.TypeParameter)
        {
            // Workaround until https://github.com/dotnet/roslyn/pull/85201 merges
            explicitCastExpression = SyntaxFactory.CastExpression(
                SyntaxFactory.IdentifierName(expressionType.Name),
                (ExpressionSyntax)readOnlyExpression);
        }
        else
        {
            var generator = SyntaxGenerator.GetGenerator(document);
            explicitCastExpression = (CastExpressionSyntax)generator.CastExpression(expressionType, readOnlyExpression);
        }

        var parethenthesizedExpression = SyntaxFactory.ParenthesizedExpression(explicitCastExpression);

        return document.WithSyntaxRoot(syntaxRoot.ReplaceNode(readOnlyExpression, parethenthesizedExpression));
    }
}
