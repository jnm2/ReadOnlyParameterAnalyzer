namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;
using TUnit.Core;

public class ReadOnlyParameterSemanticTests : AnalyzerTests<ReadOnlyParameterMutationAnalyzer>
{
    private const string AttributeSource = """
        namespace Instrumental.Annotations
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class ReadOnlyAttribute : System.Attribute
            {
                public ReadOnlyAttribute() { }
                public ReadOnlyAttribute(bool readOnly) { }
            }
        }
        """;

    private static DiagnosticResult Expected(int location, string reason, string attribute = "ReadOnly") =>
        Diagnostic().WithLocation(location).WithMessage(
            $"Parameter 'p' is marked as readonly via [{attribute}] on the parameter declaration, but it is possibly mutated by {reason}");

    private static async Task Check(string source, params DiagnosticResult[] diagnostics)
    {
        var test = new TestConfig.Test
        {
            LanguageVersion = LanguageVersion.CSharp14,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };
        test.TestState.Sources.Add(AttributeSource);
        test.SolutionTransforms.Add((solution, project) => solution.WithProjectCompilationOptions(project,
            ((CSharpCompilationOptions)solution.GetProject(project).CompilationOptions).WithAllowUnsafe(true)));
        test.ExpectedDiagnostics.AddRange(diagnostics);
        await test.RunAsync(TestContext.Current.Execution.CancellationToken);
    }

    [Test]
    public async Task All_assignment_forms_and_increment_are_forbidden()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    {|#0:p|} = 1;
                    {|#1:p|} += 2;
                    ({|#2:p|}, _) = (3, 4);
                    {|#3:p|}++;
                    --{|#4:p|};
                }
                void N([ReadOnly] string p) { {|#5:p|} ??= ""; }
            }
            """,
            Expected(0, "'=' assignment"), Expected(1, "'+=' assignment"), Expected(2, "'=' assignment"),
            Expected(3, "'++' operator"), Expected(4, "'--' operator"), Expected(5, "'??=' assignment"));
    }

    [Test]
    public async Task Boolean_attribute_argument_is_honored()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void A([ReadOnly(true)] int p) { {|#0:p|} = 1; }
                void B([ReadOnly(false)] int p) { p = 1; }
                void D([ReadOnly(readOnly: true)] int p) { {|#1:p|} = 1; }
            }
            """, Expected(0, "'=' assignment", "ReadOnly(true)"), Expected(1, "'=' assignment", "ReadOnly(readOnly: true)"));
    }

    [Test]
    public async Task Writable_ref_arguments_locals_returns_and_reassignment_are_forbidden()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                static void R(ref int x) { }
                static void O(out int x) { x = 0; }
                static void I(in int x) { }
                void M([ReadOnly] S p)
                {
                    R(ref {|#0:p|}.X);
                    O(out {|#1:p|}.X);
                    ref int r = ref {|#2:p|}.X;
                    r = ref {|#3:p|}.X;
                    ref readonly int read = ref p.X;
                    I(in p.X);
                }
            }
            struct S { public int X; }
            """, Expected(0, "taking a writable reference"), Expected(1, "taking a writable reference"),
            Expected(2, "taking a writable reference"), Expected(3, "taking a writable reference"));
    }

    [Test]
    public async Task Ref_parameter_annotation_protects_reference_not_referent()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref S p, ref S other)
                {
                    p = default;
                    p.X++;
                    p.M();
                    {|#0:p|} = ref other;
                }
                void N([ReadOnly] ref readonly S p, ref S other)
                {
                    {|#1:p|} = ref other;
                }
            }
            struct S { public int X; public void M() { } }
            """, Expected(0, "'=' assignment"), Expected(1, "'=' assignment"));
    }

    [Test]
    public async Task Nested_value_fields_are_recursive_but_reference_and_ref_fields_are_shallow()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Outer p, ref S other)
                {
                    {|#0:p|}.Nested.X = 1;
                    {|#1:p|}.Nested.M();
                    p.Reference.X = 2;
                    p.Reference.Value.M();
                }
                void N([ReadOnly] scoped Refs p, ref S other)
                {
                    p.Value.X = 1;
                    p.Value.M();
                    p.Value = default;
                    {|#2:p|}.Value = ref other;
                    ref S r = ref {|#3:p|}.Value;
                }
            }
            struct S { public int X; public void M() { } }
            struct Outer { public S Nested; public Box Reference; }
            class Box { public int X; public S Value; }
            ref struct Refs { public ref S Value; }
            """, Expected(0, "'=' assignment"), Expected(1, "invoking a non-readonly struct method 'M'"),
            Expected(2, "'=' assignment"), Expected(3, "taking a writable reference"));
    }

    [Test]
    public async Task Property_and_indexer_accessors_are_calls_but_returned_values_are_copies()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    _ = {|#0:p|}.P;
                    {|#1:p|}.P = 2;
                    _ = {|#2:p|}[0];
                    {|#3:p|}[0] = 3;
                    ({|#4:p|}.P, _) = (1, 2);
                    p.Copy.M();
                    p.Read();
                    p.ReadOnlyNested.M();
                }
            }
            struct S
            {
                public int P { get => 0; set { } }
                public int this[int i] { get => i; set { } }
                public readonly S Copy => default;
                public readonly Nested ReadOnlyNested;
                public readonly void Read() { }
                public void M() { }
            }
            struct Nested { public void M() { } }
            """, Expected(0, "invoking a non-readonly struct method 'get_P'"),
            Expected(1, "'=' assignment"),
            Expected(2, "invoking a non-readonly struct method 'get_Item'"),
            Expected(3, "'=' assignment"), Expected(4, "'=' assignment"));
    }

    [Test]
    public async Task Foreach_calls_original_receiver_but_using_and_delegate_conversion_always_copy()
    {
        await Check("""
            using Instrumental.Annotations;
            using System;
            class C
            {
                void M([ReadOnly] S p)
                {
                    foreach (var x in {|#0:p|}) { }
                    using (p) { }
                    Action a = p.M;
                    Action b = new Action(p.M);
                    _ = p.GetType();
                    _ = p.ToString();
                    _ = p.GetHashCode();
                    int[] items = [.. {|#1:p|}];
                }
            }
            struct S : IDisposable
            {
                public E GetEnumerator() => default;
                public void Dispose() { }
                public void M() { }
            }
            struct E { public bool MoveNext() => false; public int Current => 0; }
            """, Expected(0, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(1, "invoking a non-readonly struct method 'GetEnumerator'"));
    }

    [Test]
    public async Task Inline_array_indexing_conversions_and_ref_iteration_are_recursive()
    {
        await Check("""
            using Instrumental.Annotations;
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    {|#0:p|}[0].X = 1;
                    {|#1:p|}[^1].M();
                    Span<S> writable = {|#2:p|};
                    ReadOnlySpan<S> readable = p;
                    var slice = {|#3:p|}[1..];
                    ReadOnlySpan<S> readonlySlice = p[1..];
                    foreach (ref S item in p)
                    {
                        {|#4:item|}.X++;
                        {|#5:item|}.M();
                    }
                    foreach (S item in p) { item.M(); }
                    foreach (ref readonly S item in p) { item.M(); }
                }
            }
            [InlineArray(3)] struct Buffer { private S first; }
            struct S { public int X; public void M() { } }
            """, Expected(0, "'=' assignment"), Expected(1, "invoking a non-readonly struct method 'M'"),
            Expected(2, "creating a writable span"), Expected(3, "creating a writable span"),
            Expected(4, "'++' operator"), Expected(5, "invoking a non-readonly struct method 'M'"));
    }

    [Test]
    public async Task Primary_parameter_initializers_and_base_arguments_are_analyzed()
    {
        await Check("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class B(int x);
            class C([ReadOnly] Buffer p) : B({|#0:p|}[0] = 3);
            class D([ReadOnly] R p) { int x = {|#1:p|}.X = 2; }
            [InlineArray(3)] struct Buffer { private int first; }
            ref struct R { public int X; }
            """, Expected(0, "'=' assignment"), Expected(1, "'=' assignment"));
    }

    [Test]
    public async Task Await_calls_original_receiver_but_patterns_and_deconstruction_copy()
    {
        await Check("""
            using Instrumental.Annotations;
            using System.Threading.Tasks;
            using System.Runtime.CompilerServices;
            class C
            {
                async Task M([ReadOnly] S p)
                {
                    await {|#0:p|};
                    var (a, b) = p;
                    if (p is { Property: 1 }) { }
                    if (p is (1, 2)) { }
                }
            }
            struct S
            {
                public int Property => 1;
                public void Deconstruct(out int a, out int b) { a = b = 0; }
                public TaskAwaiter GetAwaiter() => Task.CompletedTask.GetAwaiter();
            }
            """, Expected(0, "invoking a non-readonly struct method 'GetAwaiter'"));
    }

    [Test]
    public async Task Ref_conditional_escapes_and_ref_returns_are_forbidden()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p, bool flag, int other)
                {
                    ref int r = ref (flag ? ref {|#0:p|} : ref other);
                    (flag ? ref {|#1:p|} : ref other) = 2;
                }
                ref int R([ReadOnly] ref int p) => ref {|#2:p|};
                ref readonly int Read([ReadOnly] ref int p) => ref p;
            }
            """, Expected(0, "taking a writable reference"), Expected(1, "'=' assignment"),
            Expected(2, "taking a writable reference"));
    }

    [Test]
    public async Task Event_accessors_and_overridden_object_methods_need_defensive_copies()
    {
        await Check("""
            using Instrumental.Annotations;
            using System;
            class C
            {
                void M([ReadOnly] S p)
                {
                    {|#0:p|}.E += Handler;
                    {|#1:p|}.E -= Handler;
                    _ = {|#2:p|}.ToString();
                    _ = {|#3:p|}.GetHashCode();
                }
                void Handler() { }
            }
            struct S
            {
                public event Action E { add { } remove { } }
                public override string ToString() => "";
                public override int GetHashCode() => 0;
            }
            """, Expected(0, "invoking a non-readonly struct method 'add_E'"),
            Expected(1, "invoking a non-readonly struct method 'remove_E'"),
            Expected(2, "invoking a non-readonly struct method 'ToString'"),
            Expected(3, "invoking a non-readonly struct method 'GetHashCode'"));
    }

    [Test]
    public async Task Defensive_copy_property_is_only_present_for_compiler_defensive_copies()
    {
        var compilation = CSharpCompilation.Create("PropertyContract",
            [CSharpSyntaxTree.ParseText(AttributeSource), CSharpSyntaxTree.ParseText("""
                using Instrumental.Annotations;
                class C
                {
                    void M([ReadOnly] S p) { p = default; p.X++; R(ref p.X); p.M(); _ = p.P; p.P = 2; p.P++; }
                    static void R(ref int x) { }
                }
                struct S { public int X; public void M() { } public int P { get => 0; set { } } }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyParameterMutationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.Execution.CancellationToken);
        await Assert.That(diagnostics.Length).IsEqualTo(7);
        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.Properties[Diagnostics.ReadOnlyParameterMutation.Properties.ParameterName]).IsEqualTo("p");
            await Assert.That(diagnostic.Properties.ContainsKey(Diagnostics.ReadOnlyParameterMutation.Properties.DefensiveCopyFix))
                .IsEqualTo(diagnostic.GetMessage().Contains("invoking a non-readonly"));
        }

    }

    [Test]
    public async Task Pointers_are_allowed_but_implicit_pinning_calls_require_copies()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                unsafe void M([ReadOnly] S p)
                {
                    S* address = &p;
                    address->X = 2;
                    fixed (int* pinned = {|#0:p|}) { }
                }
            }
            struct S
            {
                public int X;
                static int shared;
                public ref int GetPinnableReference() => ref shared;
            }
            """, Expected(0, "invoking a non-readonly struct method 'GetPinnableReference'"));
    }

    [Test]
    [Arguments("-=")]
    [Arguments("*=")]
    [Arguments("/=")]
    [Arguments("%=")]
    [Arguments("&=")]
    [Arguments("|=")]
    [Arguments("^=")]
    [Arguments("<<=")]
    [Arguments(">>=")]
    [Arguments(">>>=")]
    public async Task Remaining_compound_operators_are_forbidden(string op)
    {
        await Check($$"""
            using Instrumental.Annotations;
            class C { void M([ReadOnly] int p) { {|#0:p|} {{op}} 1; } }
            """, Expected(0, $"'{op}' assignment"));
    }

    [Test]
    public async Task Generic_virtual_object_calls_can_dispatch_without_boxing_but_GetType_always_boxes()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M<T>([ReadOnly] T p) where T : struct
                {
                    _ = {|#0:p|}.ToString();
                    _ = {|#1:p|}.GetHashCode();
                    _ = p.GetType();
                }
                void N<T>([ReadOnly] T p) { _ = p.GetType(); }
            }
            """, Expected(0, "invoking a non-readonly struct method 'ToString'"),
            Expected(1, "invoking a non-readonly struct method 'GetHashCode'"));
    }

    [Test]
    public async Task Known_length_spreads_are_copied_but_uncaptured_spreads_use_the_original_receiver()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p, Unknown q)
                {
                    int[] copied = [.. p];
                    int[] threeTemporaries = [0, 0, .. p];
                    int[] uncaptured = [0, 0, 0, .. {|#0:p|}];
                    int[] mixed = [.. {|#1:p|}, .. q];
                }
            }
            struct S
            {
                public int Length => 0;
                public E GetEnumerator() => default;
            }
            struct Unknown { public E GetEnumerator() => default; }
            struct E { public bool MoveNext() => false; public int Current => 0; }
            """, Expected(0, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(1, "invoking a non-readonly struct method 'GetEnumerator'"));
    }

    [Test]
    public async Task Ref_extension_calls_escape_but_value_extension_enumeration_copies()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p) { {|#0:p|}.Mutate(); }
                void N([ReadOnly] V p) { foreach (var x in p) { } }
            }
            struct S { }
            struct V { }
            static class Extensions
            {
                public static void Mutate(this ref S s) { }
                public static E GetEnumerator(this V s) => default;
            }
            struct E { public bool MoveNext() => false; public int Current => 0; }
            """, Expected(0, "taking a writable reference"));
    }

    [Test]
    public async Task Async_foreach_calls_original_receiver_while_await_using_and_interpolation_copy()
    {
        await Check("""
            using Instrumental.Annotations;
            using System.Threading.Tasks;
            class C
            {
                async Task M([ReadOnly] S p)
                {
                    await foreach (var item in {|#0:p|}) { }
                    await using (p) { }
                    var text = $"{p}";
                }
            }
            struct S
            {
                public E GetAsyncEnumerator() => default;
                public Task DisposeAsync() => Task.CompletedTask;
                public override string ToString() => "";
            }
            struct E
            {
                public int Current => 0;
                public Task<bool> MoveNextAsync() => Task.FromResult(false);
                public Task DisposeAsync() => Task.CompletedTask;
            }
            """, Expected(0, "invoking a non-readonly struct method 'GetAsyncEnumerator'"));
    }

    [Test]
    public async Task Interface_only_foreach_uses_constrained_call_on_original_struct()
    {
        await Check("""
            using Instrumental.Annotations;
            using System;
            using System.Collections;
            using System.Collections.Generic;
            class C
            {
                void M([ReadOnly] S p)
                {
                    foreach (var item in {|#0:p|}) { }
                    foreach (var item in (IEnumerable<int>)p) { }
                }
            }
            struct S : IEnumerable<int>
            {
                IEnumerator<int> IEnumerable<int>.GetEnumerator() => new List<int>().GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
            }
            """, Expected(0, "invoking a non-readonly struct method 'GetEnumerator'"));
    }

    [Test]
    public async Task Nullable_foreach_copies_the_underlying_value()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S? p) { foreach (var item in p) { } }
            }
            struct S { public E GetEnumerator() => default; }
            struct E { public int Current => 0; public bool MoveNext() => false; }
            """);
    }

    [Test]
    public async Task Default_interface_enumeration_boxes_instead_of_mutating_struct()
    {
        await Check("""
            using Instrumental.Annotations;
            using System.Collections;
            using System.Collections.Generic;
            class C
            {
                void M([ReadOnly] S p) { foreach (var item in p) { } }
            }
            interface I : IEnumerable<int>
            {
                IEnumerator<int> IEnumerable<int>.GetEnumerator() => new List<int>().GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
            }
            struct S : I { }
            """);
    }

    [Test]
    public async Task Generic_conditional_access_retains_storage_but_nullable_conditional_access_copies()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M<T>([ReadOnly] T p) where T : I
                {
                    {|#0:p|}?.Mutate();
                    _ = {|#1:p|}?.P;
                }
                void N<T>([ReadOnly] T p) { _ = {|#2:p|}?.ToString(); }
                void Nullable([ReadOnly] S? p) { p?.Mutate(); _ = p?.P; }
                void Reference<T>([ReadOnly] T p) where T : class, I { p?.Mutate(); }
            }
            interface I { void Mutate(); int P { get; } }
            struct S : I
            {
                public void Mutate() { }
                public int P => 0;
            }
            """, Expected(0, "invoking a non-readonly struct method 'Mutate'"),
            Expected(1, "invoking a non-readonly struct method 'get_P'"),
            Expected(2, "invoking a non-readonly struct method 'ToString'"));
    }

    [Test]
    public async Task Readonly_setters_are_allowed_but_compound_reads_can_require_a_copy()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    p.P = 1;
                    p[0] = 1;
                    {|#0:p|}.Mixed++;
                    {|#1:p|}.Mixed += 1;
                }
            }
            struct S
            {
                public readonly int P { get => 0; set { } }
                public readonly int this[int index] { get => 0; set { } }
                public int Mixed { get => 0; readonly set { } }
            }
            """, Expected(0, "invoking a non-readonly struct method 'get_Mixed'"),
            Expected(1, "invoking a non-readonly struct method 'get_Mixed'"));
    }

    [Test]
    [Arguments("public ref int Length => ref shared;")]
    [Arguments("public int Length { private get => 0; set { } }")]
    [Arguments("public static int Length => 0;")]
    [Arguments("public int Length;")]
    public async Task Invalid_length_members_do_not_make_spread_countable(string member)
    {
        await Check($$"""
            using Instrumental.Annotations;
            class C { void M([ReadOnly] S p) { int[] items = [.. {|#0:p|}]; } }
            struct S
            {
                private static int shared;
                {{member}}
                public E GetEnumerator() => default;
            }
            struct E { public int Current => 0; public bool MoveNext() => false; }
            """, Expected(0, "invoking a non-readonly struct method 'GetEnumerator'"));
    }

    [Test]
    public async Task Generic_return_type_and_extension_length_do_not_make_spread_countable()
    {
        await Check("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S<int> p) { int[] items = [.. {|#0:p|}]; }
                void N([ReadOnly] Other p) { int[] items = [.. {|#1:p|}]; }
            }
            struct S<T>
            {
                public T Length => default;
                public E GetEnumerator() => default;
            }
            struct Other { public E GetEnumerator() => default; }
            static class Extensions { extension(Other value) { public int Length => 0; } }
            struct E { public int Current => 0; public bool MoveNext() => false; }
            """, Expected(0, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(1, "invoking a non-readonly struct method 'GetEnumerator'"));
    }

    [Test]
    public async Task Valid_count_is_used_when_length_getter_is_invalid()
    {
        await Check("""
            using Instrumental.Annotations;
            class C { void M([ReadOnly] S p) { int[] items = [.. p]; } }
            struct S
            {
                private static int shared;
                public ref int Length => ref shared;
                public int Count => 0;
                public E GetEnumerator() => default;
            }
            struct E { public int Current => 0; public bool MoveNext() => false; }
            """);
    }

    [Test]
    public async Task Primary_parameter_spreads_are_analyzed_in_initializers_and_base_arguments()
    {
        await Check("""
            using Instrumental.Annotations;
            class B(int[] items);
            class C([ReadOnly] S p)
            {
                int[] items = [.. {|#0:p|}];
                int[] Items { get; } = [.. {|#4:p|}];
            }
            class D([ReadOnly] S p) : B([.. {|#1:p|}]);
            class RefLike([ReadOnly] R p)
            {
                int[] items = [.. {|#2:p|}];
            }
            class RefLikeDerived([ReadOnly] R p) : B([.. {|#3:p|}]);
            struct S { public E GetEnumerator() => default; }
            ref struct R { public E GetEnumerator() => default; }
            struct E { public int Current => 0; public bool MoveNext() => false; }
            """, Expected(0, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(1, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(2, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(3, "invoking a non-readonly struct method 'GetEnumerator'"),
            Expected(4, "invoking a non-readonly struct method 'GetEnumerator'"));
    }
}
