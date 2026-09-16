namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using TUnit.Core;

public class ReadOnlyParameterMutationCodeFixTests : AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig Config = new TestConfig()
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    private static string Source([StringSyntax("C#-Test")] string members) => TestSource.Markup("""
        using System;
        using System.Linq.Expressions;
        using Instrumental.Annotations;
        class C([ReadOnly] S p)
        {
        MEMBERS
        }
        struct S
        {
            public int M() => 1;
            public void V() { }
        }
        """).Replace("MEMBERS", string.Join(Environment.NewLine, members.Split('\n').Select(line => "    " + line.TrimEnd('\r'))));

    [Test]
    [Arguments("int M() => {|IRP0001:p|}.M();", """
        int M()
        {
            var pCopy = p;
            return pCopy.M();
        }
        """)]
    [Arguments("""
        struct Outer { public S Field; }
        void M([ReadOnly] Outer value) => {|IRP0001:value|}.Field.V();
        """, """
        struct Outer { public S Field; }
        void M([ReadOnly] Outer value)
        {
            var valueCopy = value;
            valueCopy.Field.V();
        }
        """)]
    [Arguments("""
        struct Outer { public int P { get { return 1; } } }
        int M([ReadOnly] Outer value) => {|IRP0001:value|}.P;
        """, """
        struct Outer { public int P { get { return 1; } } }
        int M([ReadOnly] Outer value)
        {
            var valueCopy = value;
            return valueCopy.P;
        }
        """)]
    [Arguments("async System.Threading.Tasks.Task<int> M() => {|IRP0001:p|}.M();", """
        async System.Threading.Tasks.Task<int> M()
        {
            var pCopy = p;
            return pCopy.M();
        }
        """)]
    [Arguments("async System.Threading.Tasks.Task M() => {|IRP0001:p|}.V();", """
        async System.Threading.Tasks.Task M()
        {
            var pCopy = p;
            pCopy.V();
        }
        """)]
    [Arguments("""
        int P
        {
            set => {|IRP0001:p|}.V();
        }
        """, """
        int P
        {
            set
            {
                var pCopy = p;
                pCopy.V();
            }
        }
        """)]
    [Arguments("""
        void M()
        {
            int Local() => {|IRP0001:p|}.M();
            Console.WriteLine(Local());
        }
        """, """
        void M()
        {
            int Local()
            {
                var pCopy = p;
                return pCopy.M();
            }
            Console.WriteLine(Local());
        }
        """)]
    [Arguments("int P => {|IRP0001:p|}.M();", """
        int P
        {
            get
            {
                var pCopy = p;
                return pCopy.M();
            }
        }
        """)]
    [Arguments("""
        int P
        {
            get => {|IRP0001:p|}.M();
        }
        """, """
        int P
        {
            get
            {
                var pCopy = p;
                return pCopy.M();
            }
        }
        """)]
    [Arguments("""
        void M()
        {
            Func<int> f = () => {|IRP0001:p|}.M();
        }
        """, """
        void M()
        {
            Func<int> f = () =>
            {
                var pCopy = p;
                return pCopy.M();
            };
        }
        """)]
    [Arguments("""
        void M()
        {
            Action f = () => {|IRP0001:p|}.V();
        }
        """, """
        void M()
        {
            Action f = () =>
            {
                var pCopy = p;
                pCopy.V();
            };
        }
        """)]
    [Arguments("""
        int M()
        {
            return {|IRP0001:p|}.M();
        }
        """, """
        int M()
        {
            var pCopy = p;
            return pCopy.M();
        }
        """)]
    [Arguments("""
        void M()
        {
            int value = {|IRP0001:p|}.M();
        }
        """, """
        void M()
        {
            var pCopy = p;
            int value = pCopy.M();
        }
        """)]
    [Arguments("""
        void M()
        {
            int value;
            value = {|IRP0001:p|}.M();
        }
        """, """
        void M()
        {
            int value;
            var pCopy = p;
            value = pCopy.M();
        }
        """)]
    [Arguments("""
        void M(bool condition)
        {
            if (condition)
                {|IRP0001:p|}.V();
        }
        """, """
        void M(bool condition)
        {
            if (condition)
            {
                var pCopy = p;
                pCopy.V();
            }
        }
        """)]
    [Arguments("""
        void M(bool condition)
        {
            while (condition)
                {|IRP0001:p|}.V();
        }
        """, """
        void M(bool condition)
        {
            while (condition)
            {
                var pCopy = p;
                pCopy.V();
            }
        }
        """)]
    [Arguments("""
        void M()
        {
            if ({|IRP0001:p|}.M() > 0)
                Console.WriteLine();
        }
        """, """
        void M()
        {
            var pCopy = p;
            if (pCopy.M() > 0)
                Console.WriteLine();
        }
        """)]
    public async Task Safe_contexts([StringSyntax("C#-Test")] string source, [StringSyntax("C#-Test")] string fixedSource)
    {
        await Config.RunTestAsync(Source(source), Source(fixedSource));
    }

    [Test]
    [Arguments("int M(bool b) => b ? {|IRP0001:p|}.M() : 0;")]
    [Arguments("bool M(bool b) => b && {|IRP0001:p|}.M() > 0;")]
    [Arguments("bool M(bool b) => b || {|IRP0001:p|}.M() > 0;")]
    [Arguments("int M(int? n) => n ?? {|IRP0001:p|}.M();")]
    [Arguments("int M() => Environment.TickCount + {|IRP0001:p|}.M();")]
    [Arguments("void M() { Console.WriteLine(\"{0}\", {|IRP0001:p|}.M()); }")]
    [Arguments("void M() { while ({|IRP0001:p|}.M() > 0) { } }")]
    [Arguments("void M() { for (; {|IRP0001:p|}.M() > 0;) { } }")]
    [Arguments("void M() { do { } while ({|IRP0001:p|}.M() > 0); }")]
    [Arguments("void M() { int a = Environment.TickCount, b = {|IRP0001:p|}.M(); }")]
    [Arguments("Expression<Func<int>> M() => () => {|IRP0001:p|}.M();")]
    [Arguments("Expression<Func<Func<int>>> M() => () => () => {|IRP0001:p|}.M();")]
    [Arguments("int[] values = new int[1]; void M() { values[Environment.TickCount] = {|IRP0001:p|}.M(); }")]
    public async Task Unsafe_hoisting_is_not_offered([StringSyntax("C#-Test")] string members)
    {
        await Config.RunTestAsync(Source(members));
    }

    [Test]
    public async Task Fix_all_uses_distinct_names_and_avoids_nested_scope_collisions()
    {
        await Config.RunTestAsync(Source("""
            void M()
            {
                {|IRP0001:p|}.V();
                {|IRP0001:p|}.V();
                {
                    int pCopy = 0;
                    Console.WriteLine(pCopy);
                }
            }
            """), Source("""
            void M()
            {
                var pCopy1 = p;
                pCopy1.V();
                var pCopy2 = p;
                pCopy2.V();
                {
                    int pCopy = 0;
                    Console.WriteLine(pCopy);
                }
            }
            """));
    }

    [Test]
    public async Task Missing_defensive_copy_property_never_offers_fix()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Test", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument("Test.cs", TestSource.Text("class C { void M(S p) { p.V(); } } struct S { public void V() { } }"));
        var root = await document.GetSyntaxRootAsync();
        var receiver = root.DescendantNodes().OfType<IdentifierNameSyntax>().Single(node => node.Identifier.ValueText == "p");
        var diagnostic = Microsoft.CodeAnalysis.Diagnostic.Create(Diagnostics.ReadOnlyParameterMutation.Descriptor,
            receiver.GetLocation(), ImmutableDictionary<string, string>.Empty
                .Add(Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName, "p"), "p", "[ReadOnly]", "invocation");
        var actions = new List<CodeAction>();
        await new ReadOnlyParameterMutationCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), default));
        if (actions.Count != 0)
            throw new InvalidOperationException("A defensive copy was offered without the diagnostic's opt-in property.");
    }

    [Test]
    [Arguments("+=")]
    [Arguments("-=")]
    public async Task Event_accessor_copy_precedes_handler_evaluation([StringSyntax("C#")] string operation)
    {
        var source = Source("""
            struct Events { public event Action E { add { } remove { } } }
            Action Handler() => null;
            void M([ReadOnly] Events value)
            {
                {|IRP0001:value|}.E OP Handler();
            }
            """).Replace("OP", operation);
        var fixedSource = Source("""
            struct Events { public event Action E { add { } remove { } } }
            Action Handler() => null;
            void M([ReadOnly] Events value)
            {
                var valueCopy = value;
                valueCopy.E OP Handler();
            }
            """).Replace("OP", operation);
        await Config.RunTestAsync(source, fixedSource);
    }

    [Test]
    [Arguments("var item")]
    [Arguments("var (x, y)")]
    public async Task Foreach_collection_is_copied_once([StringSyntax("C#")] string variable)
    {
        var source = Source("""
            struct Sequence
            {
                public Enumerator GetEnumerator() => new Enumerator();
            }
            struct Enumerator
            {
                public (int, int) Current => (0, 0);
                public bool MoveNext() => false;
            }
            void M([ReadOnly] Sequence value, bool condition)
            {
                if (condition)
                    foreach (VARIABLE in {|IRP0001:value|}) { }
            }
            """).Replace("VARIABLE", variable);
        var fixedSource = Source("""
            struct Sequence
            {
                public Enumerator GetEnumerator() => new Enumerator();
            }
            struct Enumerator
            {
                public (int, int) Current => (0, 0);
                public bool MoveNext() => false;
            }
            void M([ReadOnly] Sequence value, bool condition)
            {
                if (condition)
                {
                    var valueCopy = value;
                    foreach (VARIABLE in valueCopy) { }
                }
            }
            """).Replace("VARIABLE", variable);
        await Config.RunTestAsync(source, fixedSource);
    }

    [Test]
    [Arguments("""
        void M([ReadOnly] R value)
        {
            {|IRP0001:value|}.V();
        }
        """, """
        void M([ReadOnly] R value)
        {
            var valueCopy = value;
            valueCopy.V();
        }
        """)]
    [Arguments("int M([ReadOnly] R value) => {|IRP0001:value|}.M();", """
        int M([ReadOnly] R value)
        {
            var valueCopy = value;
            return valueCopy.M();
        }
        """)]
    [Arguments("""
        int M([ReadOnly] R value)
        {
            int result = {|IRP0001:value|}.M();
            return result;
        }
        """, """
        int M([ReadOnly] R value)
        {
            var valueCopy = value;
            int result = valueCopy.M();
            return result;
        }
        """)]
    public async Task Ref_like_receiver_in_synchronous_body([StringSyntax("C#-Test")] string source, [StringSyntax("C#-Test")] string fixedSource)
    {
        var declaration = TestSource.Code("ref struct R { public void V() { } public int M() => 1; }\n");
        await Config.RunTestAsync(Source(declaration + source), Source(declaration + fixedSource));
    }

    [Test]
    public async Task Ref_like_copy_must_not_narrow_assignment_escape_scope()
    {
        await Config.AddSource("UnscopedRefAttribute.cs", """
            namespace System.Diagnostics.CodeAnalysis;
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class UnscopedRefAttribute : System.Attribute { }
            """).RunTestAsync(Source("""
            ref struct R
            {
                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public R Borrow() => this;
                public void V() { }
            }
            void M([ReadOnly] R value, bool condition)
            {
                scoped R result = default;
                if (condition)
                    result = {|IRP0001:value|}.Borrow();
                result.V();
            }
            """));
    }

    [Test]
    public async Task Allows_ref_struct_parameter_supports_safe_synchronous_copy()
    {
        var test = new TestConfig.Test
        {
            LanguageVersion = LanguageVersion.Preview,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = TestSource.Markup("""
                using Instrumental.Annotations;
                class C
                {
                    void M<T>([ReadOnly] T value) where T : I, allows ref struct
                    {
                        {|IRP0001:value|}.V();
                    }
                }
                interface I { void V(); }
                namespace Instrumental.Annotations
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public sealed class ReadOnlyAttribute : System.Attribute { }
                }
                """),
            FixedCode = TestSource.Markup("""
                using Instrumental.Annotations;
                class C
                {
                    void M<T>([ReadOnly] T value) where T : I, allows ref struct
                    {
                        var valueCopy = value;
                        valueCopy.V();
                    }
                }
                interface I { void V(); }
                namespace Instrumental.Annotations
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public sealed class ReadOnlyAttribute : System.Attribute { }
                }
                """),
        };
        await test.RunAsync(TestContext.Current.Execution.CancellationToken);
    }

    [Test]
    public async Task Allows_ref_struct_parameter_copy_respects_escape_scope()
    {
        var test = new TestConfig.Test
        {
            LanguageVersion = LanguageVersion.Preview,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = TestSource.Markup("""
                using Instrumental.Annotations;
                using System.Diagnostics.CodeAnalysis;
                class C
                {
                    void M<T>([ReadOnly] T value, bool condition) where T : I, allows ref struct
                    {
                        scoped R result = default;
                        if (condition)
                            result = {|IRP0001:value|}.Borrow();
                    }
                }
                ref struct R { }
                interface I { [UnscopedRef] R Borrow(); }
                namespace Instrumental.Annotations
                {
                    [System.AttributeUsage(System.AttributeTargets.Parameter)]
                    public sealed class ReadOnlyAttribute : System.Attribute { }
                }
                """),
        };
        await test.RunAsync(TestContext.Current.Execution.CancellationToken);
    }
}
