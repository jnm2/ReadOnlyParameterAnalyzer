using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Immutable;

namespace Instrumental.ReadOnlyParameterAnalyzer;

[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterMutationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [Diagnostics.ReadOnlyParameterMutation.Descriptor.Id];

    public override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(async (context, document, diagnostics) =>
    {
        // Track receivers through preceding edits, including rewrites of their containing body.
        // Sequential fixes also avoid independently generated locals colliding during Fix All.
        var root = await document.GetSyntaxRootAsync(context.CancellationToken);
        var tracked = diagnostics.OrderBy(d => d.Location.SourceSpan.Start).Select(d => (
            Diagnostic: d,
            Node: root.FindNode(d.Location.SourceSpan, getInnermostNodeForTie: true),
            Annotation: new SyntaxAnnotation())).ToArray();
        root = root.ReplaceNodes(tracked.Select(t => t.Node).Distinct(), (original, rewritten) =>
            rewritten.WithAdditionalAnnotations(tracked.Where(t => t.Node == original).Select(t => t.Annotation)));
        document = document.WithSyntaxRoot(root);
        foreach (var item in tracked)
        {
            root = await document.GetSyntaxRootAsync(context.CancellationToken);
            var node = root.GetAnnotatedNodes(item.Annotation).FirstOrDefault();
            if (node is not null)
                document = await ApplyDefensiveCopyFixAsync(document,
                    Diagnostic.Create(item.Diagnostic.Descriptor, node.GetLocation(), item.Diagnostic.Properties),
                    context.CancellationToken);
        }
        return document;
    });

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken);
        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryGetTarget(root, model, diagnostic, out var receiver, out _, out _))
            {
                if (CanBeRefLike(model.GetTypeInfo(receiver).Type)
                    && ReferenceEquals(context.Document, await ApplyDefensiveCopyFixAsync(context.Document, diagnostic, context.CancellationToken)))
                    continue;
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Create a temporary defensive copy",
                        createChangedDocument: cancellationToken => ApplyDefensiveCopyFixAsync(context.Document, diagnostic, cancellationToken),
                        equivalenceKey: Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix),
                    diagnostic);
            }
        }
    }

    private static bool TryGetTarget(SyntaxNode root, SemanticModel model, Diagnostic diagnostic,
        out ExpressionSyntax receiver, out SyntaxNode target, out bool returnsValue)
    {
        receiver = null;
        target = null;
        returnsValue = false;
        if (!diagnostic.Properties.ContainsKey(Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix))
            return false;

        receiver = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as ExpressionSyntax;
        if (receiver is null || model.GetTypeInfo(receiver).Type is not { } type
            || type.TypeKind == TypeKind.Error)
            return false;
        // Ref-like temporaries cannot survive suspension. Decline async/iterator bodies rather
        // than introducing a local whose lifetime could cross an await or yield.
        if (CanBeRefLike(type) && receiver.Ancestors().Any(node =>
            node is AwaitExpressionSyntax || node.ChildTokens().Any(token => token.IsKind(SyntaxKind.AsyncKeyword))
            || node is BlockSyntax block && block.DescendantNodes().Any(child => child is YieldStatementSyntax)))
            return false;
        if (receiver.Ancestors().OfType<LambdaExpressionSyntax>().Any(lambda =>
            model.GetTypeInfo(lambda).ConvertedType is INamedTypeSymbol
            { Name: "Expression", ContainingNamespace: { } ns } && ns.ToDisplayString() == "System.Linq.Expressions"))
            return false;

        // A copy may only move across nodes where its receiver is evaluated first and
        // unconditionally. In particular, never cross arguments, conditional arms, or loop tests.
        SyntaxNode current = receiver;
        while (true)
        {
            switch (current.Parent)
            {
                case ParenthesizedExpressionSyntax:
                case CastExpressionSyntax:
                case PrefixUnaryExpressionSyntax unary when !unary.IsKind(SyntaxKind.PreIncrementExpression)
                    && !unary.IsKind(SyntaxKind.PreDecrementExpression):
                case AwaitExpressionSyntax:
                    current = current.Parent;
                    continue;
                case MemberAccessExpressionSyntax member when member.Expression == current:
                    current = member;
                    continue;
                case InvocationExpressionSyntax invocation when invocation.Expression == current:
                    current = invocation;
                    continue;
                case BinaryExpressionSyntax binary when binary.Left == current:
                    current = binary;
                    continue;
                case ConditionalExpressionSyntax conditional when conditional.Condition == current:
                    current = conditional;
                    continue;
                case AssignmentExpressionSyntax assignment when assignment.Right == current
                    && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && assignment.Left is IdentifierNameSyntax identifier
                    && model.GetSymbolInfo(identifier).Symbol is ILocalSymbol { RefKind: RefKind.None }:
                    current = assignment;
                    continue;
                case AssignmentExpressionSyntax assignment when assignment.Left == current
                    && assignment.Kind() is SyntaxKind.AddAssignmentExpression or SyntaxKind.SubtractAssignmentExpression
                    && model.GetSymbolInfo(assignment.Left).Symbol is IEventSymbol:
                    current = assignment;
                    continue;
                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } equals
                    when variable.Parent is VariableDeclarationSyntax declaration
                    && declaration.Variables[0] == variable
                    && declaration.Parent is LocalDeclarationStatementSyntax local
                    && local.UsingKeyword.IsKind(SyntaxKind.None):
                    target = local;
                    break;
                case ExpressionStatementSyntax statement:
                    target = statement;
                    break;
                case ReturnStatementSyntax statement when current is not RefExpressionSyntax:
                    target = statement;
                    break;
                case ThrowStatementSyntax statement:
                    target = statement;
                    break;
                case IfStatementSyntax statement when statement.Condition == current:
                    target = statement;
                    break;
                case SwitchStatementSyntax statement when statement.Expression == current:
                    target = statement;
                    break;
                case CommonForEachStatementSyntax statement when statement.Expression == current:
                    target = statement;
                    break;
                case ArrowExpressionClauseSyntax arrow:
                    target = arrow;
                    var symbol = model.GetDeclaredSymbol(arrow.Parent);
                    var method = symbol as IMethodSymbol;
                    if (symbol is IPropertySymbol property)
                    {
                        if (property.ReturnsByRef || property.ReturnsByRefReadonly)
                            return false;
                        returnsValue = true;
                    }
                    else if (method is not null)
                    {
                        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                            return false;
                        if (method.IsAsync && !IsStandardTask(method.ReturnType) && !method.ReturnsVoid)
                            return false;
                        returnsValue = !method.ReturnsVoid && !(method.IsAsync
                            && method.ReturnType is INamedTypeSymbol { Arity: 0 });
                    }
                    else
                        return false;
                    return arrow.Parent is MethodDeclarationSyntax or LocalFunctionStatementSyntax
                        or AccessorDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax
                        or ConstructorDeclarationSyntax or DestructorDeclarationSyntax;
                case LambdaExpressionSyntax lambda when lambda.Body == current:
                    target = lambda;
                    if (model.GetTypeInfo(lambda).ConvertedType is not INamedTypeSymbol
                        { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: { } invoke })
                        return false;
                    if (invoke.ReturnsByRef || invoke.ReturnsByRefReadonly)
                        return false;
                    if (lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword)
                        && !invoke.ReturnsVoid && !IsStandardTask(invoke.ReturnType))
                        return false;
                    returnsValue = !invoke.ReturnsVoid && !(lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword)
                        && invoke.ReturnType is INamedTypeSymbol { Arity: 0 });
                    return true;
                default:
                    return false;
            }

            return target.Parent is BlockSyntax or SwitchSectionSyntax
                or IfStatementSyntax or ElseClauseSyntax or WhileStatementSyntax
                or ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax
                or DoStatementSyntax or UsingStatementSyntax or LockStatementSyntax or LabeledStatementSyntax;
        }
    }

    private static bool IsStandardTask(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Task" or "ValueTask" } named
        && named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";

    private static bool CanBeRefLike(ITypeSymbol type) =>
        type.IsRefLikeType || type is ITypeParameterSymbol { AllowsRefLikeType: true };

    private static async Task<Document> ApplyDefensiveCopyFixAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (!TryGetTarget(root, model, diagnostic, out var receiver, out var target, out var returnsValue))
            return document;

        async Task<Document> CompleteAsync(SyntaxNode updatedRoot)
        {
            var updated = document.WithSyntaxRoot(updatedRoot);
            if (CanBeRefLike(model.GetTypeInfo(receiver, cancellationToken).Type))
            {
                // Ref safety depends on the invoked member's escape contract, not just syntax.
                // Let the compiler reject copies that narrow a returned value's escape scope.
                var updatedModel = await updated.GetSemanticModelAsync(cancellationToken);
                if (updatedModel.GetDiagnostics(cancellationToken: cancellationToken)
                    .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                    return document;
            }
            return updated;
        }

        var baseName = (diagnostic.Properties.TryGetValue(Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName, out var parameterName)
            ? parameterName : "value") + "Copy";
        var names = new HashSet<string>(root.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText));
        var name = baseName;
        for (var suffix = 1; names.Contains(name) || model.LookupSymbols(receiver.SpanStart, name: name).Length != 0; suffix++)
            name = baseName + suffix;

        var copy = SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(
            SyntaxFactory.IdentifierName("var"),
            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(name)
                .WithInitializer(SyntaxFactory.EqualsValueClause(receiver.WithoutTrivia())))));
        var rewritten = target.ReplaceNode(receiver, SyntaxFactory.IdentifierName(name).WithTriviaFrom(receiver));
        SyntaxNode replacement;
        if (target is StatementSyntax statement)
        {
            var statements = new StatementSyntax[] { copy.WithLeadingTrivia(statement.GetLeadingTrivia()), ((StatementSyntax)rewritten).WithoutLeadingTrivia() };
            if (statement.Parent is BlockSyntax block)
                return await CompleteAsync(root.ReplaceNode(block, block.WithStatements(
                    block.Statements.Remove(statement).InsertRange(block.Statements.IndexOf(statement), statements))
                    .WithAdditionalAnnotations(Formatter.Annotation)));
            if (statement.Parent is SwitchSectionSyntax section)
                return await CompleteAsync(root.ReplaceNode(section, section.WithStatements(
                    section.Statements.Remove(statement).InsertRange(section.Statements.IndexOf(statement), statements))
                    .WithAdditionalAnnotations(Formatter.Annotation)));
            replacement = SyntaxFactory.Block(statements);
        }
        else
        {
            if (rewritten is ArrowExpressionClauseSyntax originalArrow)
                copy = copy.WithLeadingTrivia(originalArrow.ArrowToken.LeadingTrivia
                    .AddRange(originalArrow.ArrowToken.TrailingTrivia));
            var expression = rewritten is ArrowExpressionClauseSyntax arrow ? arrow.Expression : (ExpressionSyntax)((LambdaExpressionSyntax)rewritten).Body;
            StatementSyntax bodyStatement = expression is ThrowExpressionSyntax thrown
                ? SyntaxFactory.ThrowStatement(thrown.Expression)
                : returnsValue ? SyntaxFactory.ReturnStatement(expression) : SyntaxFactory.ExpressionStatement(expression);
            var body = SyntaxFactory.Block(copy, bodyStatement).WithOpenBraceToken(
                SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
            if (rewritten is LambdaExpressionSyntax lambda)
                replacement = lambda.WithBody(body);
            else
            {
                target = target.Parent;
                replacement = target switch
                {
                    MethodDeclarationSyntax method => method.WithExpressionBody(null).WithSemicolonToken(default).WithBody(body),
                    LocalFunctionStatementSyntax local => local.WithExpressionBody(null).WithSemicolonToken(default).WithBody(body),
                    AccessorDeclarationSyntax accessor => accessor.WithExpressionBody(null).WithSemicolonToken(default).WithBody(body),
                    ConstructorDeclarationSyntax constructor => constructor.WithExpressionBody(null).WithSemicolonToken(default).WithBody(body),
                    DestructorDeclarationSyntax destructor => destructor.WithExpressionBody(null).WithSemicolonToken(default).WithBody(body),
                    PropertyDeclarationSyntax property => property.WithExpressionBody(null).WithSemicolonToken(default)
                        .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(body)))),
                    IndexerDeclarationSyntax indexer => indexer.WithExpressionBody(null).WithSemicolonToken(default)
                        .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(body)))),
                    _ => target,
                };
                replacement = replacement.WithTrailingTrivia(target.GetTrailingTrivia());
            }
        }
        return await CompleteAsync(root.ReplaceNode(target, replacement.WithAdditionalAnnotations(Formatter.Annotation)));
    }
}
