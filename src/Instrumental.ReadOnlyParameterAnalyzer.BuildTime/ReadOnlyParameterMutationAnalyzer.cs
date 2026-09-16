using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            OperationKind.Increment,
            OperationKind.Decrement,
            OperationKind.Argument,
            OperationKind.PropertyReference,
            OperationKind.EventAssignment,
            OperationKind.Conversion,
            OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(AnalyzeRef, SyntaxKind.RefExpression);
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement);
        context.RegisterSyntaxNodeAction(AnalyzeInlineSlice, SyntaxKind.ElementAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
        context.RegisterSyntaxNodeAction(AnalyzeFixed, SyntaxKind.FixedStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSpread, SyntaxKind.SpreadElement);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IAssignmentOperation assignmentOperation:
                var operatorText = assignmentOperation.Syntax is AssignmentExpressionSyntax syntax ? syntax.OperatorToken.ValueText : "=";
                AnalyzeTarget(context, assignmentOperation.Target, $"'{operatorText}' assignment",
                    assignmentOperation is ISimpleAssignmentOperation { IsRef: true });
                break;
            case IIncrementOrDecrementOperation increment:
                AnalyzeTarget(context, increment.Target, $"'{(increment.Kind == OperationKind.Increment ? "++" : "--")}' operator");
                break;
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                AnalyzeTarget(context, argument.Value, "taking a writable reference", includeRef: true);
                break;
            case IPropertyReferenceOperation property:
                // A property value is a copy; only the accessor's receiver is relevant.
                var target = (IOperation)property;
                while (target.Parent is ITupleOperation or IParenthesizedOperation)
                    target = target.Parent;
                var write = target.Parent is IAssignmentOperation assignment && assignment.Target == target
                    || target.Parent is IIncrementOrDecrementOperation;
                if (!write || property.Property.ReturnsByRef || property.Property.ReturnsByRefReadonly
                    || property.Property.SetMethod?.IsReadOnly == true
                        && target.Parent is ICompoundAssignmentOperation or ICoalesceAssignmentOperation or IIncrementOrDecrementOperation)
                    AnalyzeCall(context, property.Instance, property.Property.GetMethod);
                break;
            case IEventAssignmentOperation { EventReference: IEventReferenceOperation eventReference } eventAssignment:
                AnalyzeCall(context, eventReference.Instance, eventAssignment.Adds ? eventReference.Event.AddMethod : eventReference.Event.RemoveMethod);
                break;
            case IConversionOperation conversion when IsSpan(conversion.Type, "Span") && conversion.GetConversion().IsInlineArray:
                AnalyzeTarget(context, conversion.Operand, "creating a writable span");
                break;
            case IInvocationOperation invocationOperation:
                AnalyzeCall(context, invocationOperation.Instance, invocationOperation.TargetMethod);
                break;
        }
    }

    private static void AnalyzeTarget(OperationAnalysisContext context, IOperation target, string reason, bool includeRef = false)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements)
                AnalyzeTarget(context, element, reason, includeRef);
        }
        else if (target is IPropertyReferenceOperation { Instance.Type.IsReferenceType: false } property
            && !property.Property.ReturnsByRef && !property.Property.ReturnsByRefReadonly)
        {
            if (property.Property.SetMethod?.IsReadOnly != true)
                AnalyzeTarget(context, property.Instance, reason);
        }
        else
            foreach (var creator in GetRoots(target, includeRef))
                context.ReportDiagnostic(creator.Create(reason));
    }

    private static bool NeedsCopy(IOperation receiver, IMethodSymbol method) =>
        receiver?.Type is { IsReferenceType: false } && method is { IsStatic: false, IsReadOnly: false, ReducedFrom: null }
        && !(method.ContainingType.TypeKind == TypeKind.Interface
            && receiver.Type is INamedTypeSymbol { IsValueType: true } concrete
            && concrete.FindImplementationForInterfaceMember(method) is IMethodSymbol { ContainingType.TypeKind: TypeKind.Interface })
        // Inherited Object/ValueType methods box a concrete struct regardless of readonly.
        && !(method.ContainingType.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
            && (receiver.Type is not ITypeParameterSymbol || !(method.IsVirtual || method.IsOverride)));

    private static void AnalyzeCall(OperationAnalysisContext context, IOperation receiver, IMethodSymbol method)
    {
        // IOperation exposes the implicit boxing conversion even when the emitter
        // replaces it with a constrained virtual call on the original storage.
        if (receiver is IConversionOperation { IsImplicit: true } conversion && conversion.GetConversion().IsBoxing)
            receiver = conversion.Operand;
        if (receiver is IConditionalAccessInstanceOperation)
        {
            for (var ancestor = receiver.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor is not IConditionalAccessOperation conditional)
                    continue;
                // Generic conditional access retains the original storage for
                // value-type instantiations; nullable conditional access copies Value.
                receiver = conditional.Operation.Type?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    ? null : conditional.Operation;
                break;
            }
        }
        if (NeedsCopy(receiver, method))
            foreach (var creator in GetRoots(receiver, false))
                context.ReportDiagnostic(creator.Create($"invoking a non-readonly struct method '{method.Name}'", defensiveCopy: true));
    }

    private static void AnalyzeRef(SyntaxNodeAnalysisContext context)
    {
        var syntax = (RefExpressionSyntax)context.Node;
        var parent = syntax.Parent;
        ISymbol target = parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } => context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken),
            AssignmentExpressionSyntax assignment => context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol,
            ReturnStatementSyntax or ArrowExpressionClauseSyntax => context.ContainingSymbol,
            _ => null
        };
        var writable = target switch
        {
            ILocalSymbol local => local.RefKind == RefKind.Ref,
            IFieldSymbol field => field.RefKind == RefKind.Ref,
            IParameterSymbol parameter => parameter.RefKind is RefKind.Ref or RefKind.Out,
            IMethodSymbol method => method.ReturnsByRef,
            IPropertySymbol property => property.ReturnsByRef,
            _ => false
        };
        if (writable)
            foreach (var creator in GetRoots(GetExpressionOperation(context.SemanticModel, syntax.Expression), true))
                context.ReportDiagnostic(creator.Create("taking a writable reference"));
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context)
    {
        var syntax = (CommonForEachStatementSyntax)context.Node;
        var expression = GetExpressionOperation(context.SemanticModel, syntax.Expression);
        // Nullable foreach invokes the underlying value's enumerator on a Value
        // property result, which is a copy even when the nullable is writable.
        if (expression?.Type?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return;
        var method = context.SemanticModel.GetForEachStatementInfo(syntax).GetEnumeratorMethod;
        AnalyzeImplicitCall(context, expression, method);
    }

    private static void AnalyzeAwait(SyntaxNodeAnalysisContext context)
    {
        var syntax = (AwaitExpressionSyntax)context.Node;
        var expression = GetExpressionOperation(context.SemanticModel, syntax.Expression);
        var method = context.SemanticModel.GetAwaitExpressionInfo(syntax).GetAwaiterMethod;
        AnalyzeImplicitCall(context, expression, method);
    }

    private static void AnalyzeImplicitCall(SyntaxNodeAnalysisContext context, IOperation receiver, IMethodSymbol method)
    {
        var extension = method?.ReducedFrom ?? (method?.IsExtensionMethod == true ? method : null);
        if (extension?.Parameters[0].RefKind is RefKind.Ref or RefKind.Out)
        {
            foreach (var creator in GetRoots(receiver, true))
                context.ReportDiagnostic(creator.Create("taking a writable reference"));
        }
        else if (NeedsCopy(receiver, method))
        {
            foreach (var creator in GetRoots(receiver, false))
                context.ReportDiagnostic(creator.Create($"invoking a non-readonly struct method '{method.Name}'", defensiveCopy: true));
        }
    }

    private static void AnalyzeFixed(SyntaxNodeAnalysisContext context)
    {
        foreach (var variable in ((FixedStatementSyntax)context.Node).Declaration.Variables)
        {
            if (variable.Initializer?.Value is not { } expression)
                continue;
            var receiver = GetExpressionOperation(context.SemanticModel, expression);
            if (!TryGetRoot(receiver, false, out var creator))
                continue;
            // Roslyn does not expose the implicit pinning call in IOperation.
            var call = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia()),
                SyntaxFactory.IdentifierName("GetPinnableReference")));
            if (context.SemanticModel.GetSpeculativeSymbolInfo(expression.SpanStart, call, SpeculativeBindingOption.BindAsExpression).Symbol is not IMethodSymbol method)
                continue;
            if (NeedsCopy(receiver, method))
                context.ReportDiagnostic(creator.Create($"invoking a non-readonly struct method '{method.Name}'", defensiveCopy: true));
            else if (method.ReducedFrom?.Parameters[0].RefKind == RefKind.Ref)
                context.ReportDiagnostic(creator.Create("taking a writable reference"));
        }
    }

    private static void AnalyzeSpread(SyntaxNodeAnalysisContext context)
    {
        var syntax = (SpreadElementSyntax)context.Node;
        var receiver = GetExpressionOperation(context.SemanticModel, syntax.Expression);
        if (!TryGetRoot(receiver, false, out _) || SpreadIsUnconditionallyCopied(context.SemanticModel, syntax))
            return;
        // Spread enumeration uses foreach binding, but exposes no enumerator symbol.
        var loop = SyntaxFactory.ForEachStatement(SyntaxFactory.IdentifierName("var"),
            SyntaxFactory.Identifier("__item"), syntax.Expression.WithoutTrivia(), SyntaxFactory.Block());
        if (context.SemanticModel.TryGetSpeculativeSemanticModel(syntax.SpanStart, loop, out var model))
            AnalyzeImplicitCall(context, receiver, model.GetForEachStatementInfo(loop).GetEnumeratorMethod);
        else
        {
            // Field/property initializers cannot host speculative statements. Bind
            // the same foreach inside a target-typed lambda in a speculative initializer.
            // Capture/type-conversion errors do not affect foreach member lookup.
            var initializer = SyntaxFactory.EqualsValueClause(SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("global::System.Action"),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.Block(loop))))), null));
            if (context.SemanticModel.TryGetSpeculativeSemanticModel(syntax.SpanStart, initializer, out model))
            {
                var nestedLoop = initializer.DescendantNodes().OfType<ForEachStatementSyntax>().Single();
                AnalyzeImplicitCall(context, receiver, model.GetForEachStatementInfo(nestedLoop).GetEnumeratorMethod);
            }
        }
    }

    private static bool SpreadIsUnconditionallyCopied(SemanticModel model, SpreadElementSyntax spread)
    {
        if (spread.Parent is not CollectionExpressionSyntax collection)
            return false;
        var destination = model.GetTypeInfo(collection).ConvertedType;
        var standardDestination = destination is IArrayTypeSymbol
            || IsSpan(destination, "Span") || IsSpan(destination, "ReadOnlySpan")
            || destination is INamedTypeSymbol { Arity: 1 } named
                && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
                && named.Name is "List" or "IEnumerable" or "ICollection" or "IList" or "IReadOnlyCollection" or "IReadOnlyList"
            || destination?.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CollectionBuilderAttribute") == true;
        if (!standardDestination)
            return false;

        // Roslyn's known-length lowering captures elements through the last spread
        // by value, but only when it needs at most three temporaries.
        // https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Lowering/LocalRewriter/LocalRewriter_CollectionExpression.cs
        var lastSpread = -1;
        for (var i = 0; i < collection.Elements.Count; i++)
        {
            if (collection.Elements[i] is not SpreadElementSyntax element)
                continue;
            lastSpread = i;
            if (!HasKnownLength(model, element.Expression))
                return false;
        }
        return lastSpread is >= 0 and < 3;
    }

    private static bool HasKnownLength(SemanticModel model, ExpressionSyntax expression)
    {
        var type = model.GetTypeInfo(expression).Type;
        if (type is IArrayTypeSymbol || IsInlineArray(type))
            return true;
        foreach (var name in new[] { "Length", "Count" })
        {
            var access = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia()), SyntaxFactory.IdentifierName(name));
            // Match TryBindNonExtensionLengthOrCount / HasValidLengthOrCountGetter,
            // including the getter's original return type and Length-before-Count lookup.
            // https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Binder/Binder_Expressions.cs
            if (model.GetSpeculativeSymbolInfo(expression.SpanStart, access, SpeculativeBindingOption.BindAsExpression).Symbol
                is IPropertySymbol { IsStatic: false, ContainingType.IsExtension: false, GetMethod: { } getter }
                && getter.OriginalDefinition is { ReturnType.SpecialType: SpecialType.System_Int32, RefKind: RefKind.None }
                && model.IsAccessible(expression.SpanStart, getter))
                return true;
        }
        return false;
    }

    private static void AnalyzeInlineSlice(SyntaxNodeAnalysisContext context)
    {
        var syntax = (ElementAccessExpressionSyntax)context.Node;
        var receiver = GetExpressionOperation(context.SemanticModel, syntax.Expression);
        if (!IsInlineArray(receiver?.Type) || !IsSpan(context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type, "Span"))
            return;
        // A slice immediately converted to ReadOnlySpan does not expose writable storage.
        if (IsSpan(context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).ConvertedType, "ReadOnlySpan"))
            return;
        if (TryGetRoot(receiver, false, out var creator))
            context.ReportDiagnostic(creator.Create("creating a writable span"));
    }

    private static bool IsSpan(ITypeSymbol type, string name) =>
        type is INamedTypeSymbol { Arity: 1 } named && named.Name == name && named.ContainingNamespace.ToDisplayString() == "System";

    private static IOperation GetExpressionOperation(SemanticModel model, ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses)
            expression = parentheses.Expression;
        return model.GetOperation(expression);
    }

    private static bool IsInlineArray(ITypeSymbol type) =>
        type?.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InlineArrayAttribute") == true;

    private static IEnumerable<ReadOnlyParameterMutationDiagnosticCreator> GetRoots(IOperation operation, bool includeRef)
    {
        if (operation is IConditionalOperation { IsRef: true } conditional)
        {
            foreach (var root in GetRoots(conditional.WhenTrue, includeRef))
                yield return root;
            foreach (var root in GetRoots(conditional.WhenFalse, includeRef))
                yield return root;
        }
        else if (operation is IParenthesizedOperation parentheses)
        {
            foreach (var root in GetRoots(parentheses.Operand, includeRef))
                yield return root;
        }
        else if (operation is IFieldReferenceOperation { Instance.Type.IsReferenceType: false } field
            && (includeRef || field.Field.RefKind == RefKind.None) && !field.Field.IsReadOnly)
        {
            foreach (var root in GetRoots(field.Instance, false))
                yield return root;
        }
        else if (TryGetRoot(operation, includeRef, out var creator))
            yield return creator;
    }

    private readonly struct ReadOnlyParameterMutationDiagnosticCreator(
        IParameterReferenceOperation parameterReference,
        SyntaxReference applicationSyntaxReference,
        SyntaxNode location = null)
    {
        public ReadOnlyParameterMutationDiagnosticCreator At(SyntaxNode syntax) =>
            new(parameterReference, applicationSyntaxReference, syntax);

        public Diagnostic Create(string mutatedBy, bool defensiveCopy = false)
        {
            var configuredAsReadonlyVia = $"[{applicationSyntaxReference.GetSyntax()}] on the parameter declaration";

            var properties = ImmutableDictionary.CreateBuilder<string, string>();
            properties.Add(Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName, parameterReference.Parameter.Name);
            if (defensiveCopy)
                properties.Add(Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix, null);

            return Diagnostic.Create(
                Diagnostics.ReadOnlyParameterMutation.Descriptor,
                (location ?? parameterReference.Syntax).GetLocation(),
                properties.ToImmutable(),
                messageArgs: [parameterReference.Parameter.Name, configuredAsReadonlyVia, mutatedBy]);
        }
    }

    private static bool TryGetRoot(IOperation operation, bool includeRef, out ReadOnlyParameterMutationDiagnosticCreator diagnosticCreator)
    {
        while (operation is IParenthesizedOperation parenthesized)
            operation = parenthesized.Operand;

        if (operation is IParameterReferenceOperation parameterReference
            && (includeRef || parameterReference.Parameter.RefKind == RefKind.None)
            && IsReadOnlyParameter(parameterReference.Parameter, out var applicationSyntaxReference))
        {
            diagnosticCreator = new(parameterReference, applicationSyntaxReference);
            return true;
        }

        if (operation is IFieldReferenceOperation { Instance.Type.IsReferenceType: false } field
            && (includeRef || field.Field.RefKind == RefKind.None)
            && !field.Field.IsReadOnly)
            return TryGetRoot(field.Instance, false, out diagnosticCreator);

        // Inline-array access has no public dedicated operation interface. Its receiver
        // remains available through the syntax/semantic model (unlike normal indexers).
        if (operation?.Syntax is ElementAccessExpressionSyntax access && operation.SemanticModel is { } model
            && IsInlineArray(model.GetTypeInfo(access.Expression).Type)
            && !IsSpan(operation.Type, "Span") && !IsSpan(operation.Type, "ReadOnlySpan"))
            return TryGetRoot(GetExpressionOperation(model, access.Expression), false, out diagnosticCreator);

        if (operation is ILocalReferenceOperation { Local.RefKind: RefKind.Ref } local)
        {
            var declaration = local.Local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (declaration is ForEachStatementSyntax loop && local.SemanticModel is { } localModel
                && IsInlineArray(localModel.GetTypeInfo(loop.Expression).Type)
                && TryGetRoot(GetExpressionOperation(localModel, loop.Expression), false, out var root))
            {
                diagnosticCreator = root.At(local.Syntax);
                return true;
            }
        }

        diagnosticCreator = default;
        return false;
    }

    private static bool IsReadOnlyParameter(IParameterSymbol symbol, out SyntaxReference applicationSyntaxReference)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsReadOnlyParameterAttribute(attribute)
                && attribute.ApplicationSyntaxReference is { } syntaxReference
                && (attribute.ConstructorArguments.IsEmpty || attribute.ConstructorArguments[0].Value is true))
            {
                applicationSyntaxReference = syntaxReference;
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
