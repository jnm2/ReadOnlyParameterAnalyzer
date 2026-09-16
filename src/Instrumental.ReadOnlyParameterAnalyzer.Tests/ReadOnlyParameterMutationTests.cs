namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using System.Threading.Tasks;
using TUnit.Core;

public class ReadOnlyParameterMutationTests : AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                int f = {|#1:p|} = 5;
            }
            """, Diagnostic().WithLocation(1).WithMessage("Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

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
            """, Diagnostic().WithLocation(1).WithMessage("Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by invoking a non-readonly struct method 'M'"), """
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M()
                {
                    var pCopy = p;
                    pCopy.M();
                }
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
            """, Diagnostic().WithLocation(1).WithMessage("Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by invoking a non-readonly struct method 'M'"), """
            using Instrumental.Annotations;
            class C<T>([ReadOnly] T p) where T : I
            {
                void M()
                {
                    var pCopy = p;
                    pCopy.M();
                }
            }
            interface I { void M(); }
            """);
    }
}
