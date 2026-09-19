namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using TUnit.Core;

public class PointerTests : Framework.AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
        .WithReferenceAssemblies(ReferenceAssemblies.Net.Net100)
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    [Test]
    public async Task Pointer_allowed_to_readonly_primary_parameter()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                void M()
                {
                    unsafe
                    {
                        fixed (int* ptr = &p)
                        {
                            *ptr = 5;
                        }
                    }
                }
            }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_regular_parameter()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C()
            {
                void M([ReadOnly] int p)
                {
                    unsafe
                    {
                        int* ptr = &p;
                        *ptr = 5;
                    }
                }
            }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_inline_array_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p[0])
                    {
                        *ptr = 5;
                    }
                }
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_mutable_struct_field()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p.MutableFieldInStruct)
                    {
                        *ptr = 5;
                    }
                }
            }
            struct S { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_nested_inline_array_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] OuterBuffer p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p[0][0])
                    {
                        *ptr = 5;
                    }
                }
            }
            [InlineArray(2)]
            struct OuterBuffer { private InnerBuffer element; }
            [InlineArray(2)]
            struct InnerBuffer { private int element; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_inline_array_element_mutable_struct_field()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p[0].MutableFieldInStruct)
                    {
                        *ptr = 5;
                    }
                }
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_struct_inline_array_field_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] S p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p.InlineArrayFieldInStruct[0])
                    {
                        *ptr = 5;
                    }
                }
            }
            struct S { public Buffer InlineArrayFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_nested_mutable_struct_field()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p.MutableFieldInStruct.MutableFieldInStruct2)
                    {
                        *ptr = 5;
                    }
                }
            }
            struct S { public S2 MutableFieldInStruct; }
            struct S2 { public int MutableFieldInStruct2; }
            """);
    }
}
