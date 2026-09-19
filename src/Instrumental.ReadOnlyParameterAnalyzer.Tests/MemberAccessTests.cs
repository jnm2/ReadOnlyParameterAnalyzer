namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using System.Threading.Tasks;
using TUnit.Core;

public class MemberAccessTests : Framework.AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    [Test]
    public async Task ReadOnly_primary_parameter_non_readonly_struct_member_invocation()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.M();
            }
            struct S { public void M() { } }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by invoking a non-readonly struct method 'M'"), """
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => ((S)p).M();
            }
            struct S { public void M() { } }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_readonly_struct_member_invocation()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => p.M();
            }
            struct S { public readonly void M() { } }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_indirectly_readonly_struct_member_invocation()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => p.M();
            }
            readonly struct S { public void M() { } }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_class_member_invocation()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] C2 p)
            {
                void M() => p.M();
            }
            class C2 { public void M() { } }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_possibly_value_typed_generic_member_invocation()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C<T>([ReadOnly] T p) where T : I
            {
                void M() => {|#1:p|}.M();
            }
            interface I { void M(); }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by invoking a non-readonly struct method 'M'"), """
            using Instrumental.Annotations;
            class C<T>([ReadOnly] T p) where T : I
            {
                void M() => ((T)p).M();
            }
            interface I { void M(); }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_mutating_struct_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref S p) => p.Mutate();
            }
            struct S
            {
                public int Value;
                public void Mutate() => Value++;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_out_parameter_mutating_struct_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] out S p)
                {
                    p = default;
                    p.Mutate();
                }
            }
            struct S
            {
                public int Value;
                public void Mutate() => Value++;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_readonly_parameter_mutating_struct_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref readonly S p) => p.Mutate();
            }
            struct S
            {
                public int Value;
                public void Mutate() => Value++;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_in_parameter_mutating_struct_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] in S p) => p.Mutate();
            }
            struct S
            {
                public int Value;
                public void Mutate() => Value++;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_mutating_generic_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M<T>([ReadOnly] ref T p) where T : I => p.Mutate();
            }
            interface I { void Mutate(); }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_ref_field_mutating_struct_method_allowed()
    {
        await DefaultConfig
            .WithReferenceAssemblies(Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net100)
            .RunTestAsync("""
                using Instrumental.Annotations;
                class C
                {
                    void M([ReadOnly] Outer p) => p.MutableRefFieldInStruct.Mutate();
                }
                ref struct Outer { public ref Inner MutableRefFieldInStruct; }
                struct Inner
                {
                    public int Value;
                    public void Mutate() => Value++;
                }
                """);
    }
}
