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
    public async Task Pointer_allowed_to_readonly_primary_parameter_fixed_buffer_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p.FixedBuffer[0])
                    {
                        *ptr = 5;
                    }
                    fixed (int* ptr = p.FixedBuffer)
                    {
                        ptr[0] = 6;
                    }
                }
            }
            unsafe struct S { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_regular_parameter_fixed_buffer_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                unsafe void M([ReadOnly] S p)
                {
                    int* ptr = &p.FixedBuffer[0];
                    *ptr = 5;
                    int* buffer = p.FixedBuffer;
                    buffer[0] = 6;
                }
            }
            unsafe struct S { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_struct_fixed_buffer_field_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p.MutableFieldInStruct.FixedBuffer[0])
                    {
                        *ptr = 5;
                    }
                    fixed (int* ptr = p.MutableFieldInStruct.FixedBuffer)
                    {
                        ptr[0] = 6;
                    }
                }
            }
            struct S { public S2 MutableFieldInStruct; }
            unsafe struct S2 { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_ref_field_fixed_buffer_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                unsafe void M([ReadOnly] S p)
                {
                    int* ptr = &p.MutableRefFieldInStruct.FixedBuffer[0];
                    *ptr = 5;
                    int* buffer = p.MutableRefFieldInStruct.FixedBuffer;
                    buffer[0] = 6;
                }
            }
            ref struct S { public ref S2 MutableRefFieldInStruct; }
            unsafe struct S2 { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task Pointer_allowed_to_readonly_parameter_inline_array_element_fixed_buffer_element()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                unsafe void M()
                {
                    fixed (int* ptr = &p[0].FixedBuffer[0])
                    {
                        *ptr = 5;
                    }
                    fixed (int* ptr = p[0].FixedBuffer)
                    {
                        ptr[0] = 6;
                    }
                }
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            unsafe struct S { public fixed int FixedBuffer[2]; }
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

    [Test]
    public async Task ReadOnly_pointer_parameter_dereferenced_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] int* p)
                {
                    *p = 5;
                    *p += 1;
                    (*p)++;
                    --*p;
                    (*p, _) = (6, 7);
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] int* p)
                {
                    p[0] = 5;
                    p[0] += 1;
                    p[0]++;
                    --p[0];
                    (p[0], _) = (6, 7);
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_inline_array_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M([ReadOnly] Buffer* p)
                {
                    (*p)[0] = 5;
                    (*p)[0]++;
                    ((*p)[0], _) = (6, 7);
                }
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_fixed_buffer_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] S* p)
                {
                    p->FixedBuffer[0] = 5;
                    (*p).FixedBuffer[0] = 6;
                    p[0].FixedBuffer[0] = 7;
                }
            }
            unsafe struct S { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_struct_fixed_buffer_field_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] S* p)
                {
                    p->MutableFieldInStruct.FixedBuffer[0] = 5;
                    (*p).MutableFieldInStruct.FixedBuffer[0] = 6;
                    p[0].MutableFieldInStruct.FixedBuffer[0] = 7;
                }
            }
            struct S { public S2 MutableFieldInStruct; }
            unsafe struct S2 { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_inline_array_element_fixed_buffer_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M([ReadOnly] Buffer* p)
                {
                    (*p)[0].FixedBuffer[0] = 5;
                    p[0][0].FixedBuffer[0] = 6;
                }
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            unsafe struct S { public fixed int FixedBuffer[2]; }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_nested_field_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] Outer* p, int* other)
                {
                    p->MutableFieldInStruct.MutablePointerFieldInStruct = other;
                    *p->MutableFieldInStruct.MutablePointerFieldInStruct = 5;
                    p->MutableFieldInStruct.Value = 6;
                    (*p).MutableFieldInStruct.Value++;
                    (p->MutableFieldInStruct.Value, _) = (7, 8);
                }
            }
            struct Outer { public Inner MutableFieldInStruct; }
            unsafe struct Inner
            {
                public int* MutablePointerFieldInStruct;
                public int Value;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_pointer_parameter_mutating_struct_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] S* p)
                {
                    p->Mutate();
                    (*p).Mutate();
                    p[0].Mutate();
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
    public async Task ReadOnly_parameter_pointer_field_dereferenced_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] S p)
                {
                    *p.MutablePointerFieldInStruct = 5;
                    *p.MutablePointerFieldInStruct += 1;
                    (*p.MutablePointerFieldInStruct)++;
                    --*p.MutablePointerFieldInStruct;
                    (*p.MutablePointerFieldInStruct, _) = (6, 7);
                }
            }
            unsafe struct S { public int* MutablePointerFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_pointer_field_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] S p)
                {
                    p.MutablePointerFieldInStruct[0] = 5;
                    p.MutablePointerFieldInStruct[0] += 1;
                    p.MutablePointerFieldInStruct[0]++;
                    --p.MutablePointerFieldInStruct[0];
                    (p.MutablePointerFieldInStruct[0], _) = (6, 7);
                }
            }
            unsafe struct S { public int* MutablePointerFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_nested_inline_pointer_field_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] Outer p)
                {
                    *p.MutableFieldInStruct.MutablePointerFieldInStruct = 5;
                    p.MutableFieldInStruct.MutablePointerFieldInStruct[0]++;
                    (*p.MutableFieldInStruct.MutablePointerFieldInStruct, _) = (6, 7);
                }
            }
            struct Outer { public Inner MutableFieldInStruct; }
            unsafe struct Inner { public int* MutablePointerFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_nested_pointer_field_pointer_assignment_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] Outer p, int* other)
                {
                    p.MutablePointerFieldInStruct->MutablePointerFieldInStruct = other;
                    p.MutablePointerFieldInStruct->MutablePointerFieldInStruct++;
                    *p.MutablePointerFieldInStruct->MutablePointerFieldInStruct = 5;
                }
            }
            unsafe struct Outer { public Inner* MutablePointerFieldInStruct; }
            unsafe struct Inner { public int* MutablePointerFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_pointer_field_nested_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] Outer p)
                {
                    p.MutablePointerFieldInStruct->MutableFieldInStruct.Value = 5;
                    p.MutablePointerFieldInStruct->MutableFieldInStruct.Value += 1;
                    p.MutablePointerFieldInStruct->MutableFieldInStruct.Value++;
                    (p.MutablePointerFieldInStruct->MutableFieldInStruct.Value, _) = (6, 7);
                }
            }
            unsafe struct Outer { public Inner* MutablePointerFieldInStruct; }
            struct Inner { public ValueType MutableFieldInStruct; }
            struct ValueType { public int Value; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_pointer_field_mutating_struct_method_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            unsafe class C
            {
                void M([ReadOnly] Outer p)
                {
                    p.MutablePointerFieldInStruct->Mutate();
                    (*p.MutablePointerFieldInStruct).Mutate();
                    p.MutablePointerFieldInStruct[0].Mutate();
                }
            }
            unsafe struct Outer { public Inner* MutablePointerFieldInStruct; }
            struct Inner
            {
                public int Value;
                public void Mutate() => Value++;
            }
            """);
    }
}
