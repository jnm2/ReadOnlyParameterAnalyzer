namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using TUnit.Core;

public class RefTakingTests : Framework.AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
        .WithReferenceAssemblies(ReferenceAssemblies.Net.Net100)
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    [Test]
    public async Task ReadOnly_regular_parameter_ref_argument()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p) => Mutate(ref {|#1:p|});
                static void Mutate(ref int value) { }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_out_argument()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p) => Initialize(out {|#1:p|});
                static void Initialize(out int value) => value = 1;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_ref_and_out_arguments()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                void M()
                {
                    Mutate(ref {|#1:p|});
                    Initialize(out {|#2:p|});
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_local_initialization()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    ref int alias = ref {|#1:p|};
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_var_initialization()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    ref var alias = ref {|#1:p|};
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_conditional_writable_local_initialization()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p, int other, bool condition)
                {
                    ref int alias = ref (condition ? ref {|#1:p|} : ref other);
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_conditional_readonly_local_initialization_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p, int other, bool condition)
                {
                    ref readonly int alias = ref (condition ? ref p : ref other);
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_local_reassignment_rhs()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    int other = 0;
                    ref int alias = ref other;
                    alias = ref {|#1:p|};
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_out_parameter_ref_reassignment_rhs()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p, out int destination)
                {
                    destination = 0;
                    destination = ref {|#1:p|};
                    destination = 42;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_field_assignment_rhs()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    scoped Holder holder = default;
                    holder.Value = ref {|#1:p|};
                }
            }
            ref struct Holder { public ref int Value; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_constructor_ref_and_out_arguments()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    _ = new RefConsumer(ref {|#1:p|});
                    _ = new OutConsumer(out {|#2:p|});
                }
            }
            class RefConsumer { public RefConsumer(ref int value) { } }
            class OutConsumer { public OutConsumer(out int value) => value = 1; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_dynamic_constructor_ref_and_out_arguments()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p, dynamic argument)
                {
                    _ = new RefConsumer(argument, ref {|#1:p|});
                    _ = new OutConsumer(argument, out {|#2:p|});
                }
            }
            class RefConsumer { public RefConsumer(object argument, ref int value) { } }
            class OutConsumer { public OutConsumer(object argument, out int value) => value = 1; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_delegate_ref_and_out_arguments()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            delegate void RefConsumer(ref int value);
            delegate void OutConsumer(out int value);
            class C
            {
                void M([ReadOnly] int p, RefConsumer mutate, OutConsumer initialize)
                {
                    mutate(ref {|#1:p|});
                    initialize(out {|#2:p|});
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_this_ref_extension_receiver()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            static class Extensions
            {
                public static void Mutate(this ref int value) { }
            }
            class C
            {
                void M([ReadOnly] int p) => {|#1:p|}.Mutate();
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_dynamic_ref_and_out_arguments()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p, dynamic receiver)
                {
                    receiver.Mutate(ref {|#1:p|});
                    receiver.Initialize(out {|#2:p|});
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_parenthesized_ref_argument()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p) => Mutate(ref ({|#1:p|}));
                static void Mutate(ref int value) { }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_regular_parameter_in_argument_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p) => Read(in p);
                static void Read(in int value) { }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_readonly_formal_arguments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    Read(ref p);
                    Read(in p);
                }
                static void Read(ref readonly int value) { }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_this_in_extension_receiver_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            static class Extensions
            {
                public static void Read(this in int value) { }
            }
            class C
            {
                void M([ReadOnly] int p) => p.Read();
            }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_readonly_local_initialization_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    ref readonly int alias = ref p;
                    ref readonly var inferredAlias = ref p;
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_readonly_local_reassignment_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    int other = 0;
                    ref readonly int alias = ref other;
                    alias = ref p;
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_ref_readonly_field_assignment_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p)
                {
                    scoped Holder holder = default;
                    holder.Value = ref p;
                }
            }
            ref struct Holder { public ref readonly int Value; }
            """);
    }

    [Test]
    public async Task ReadOnly_reference_type_parameter_writable_aliases_disallowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] object p)
                {
                    Mutate(ref {|#1:p|});
                    Initialize(out {|#2:p|});
                    ref object alias = ref {|#3:p|};
                }
                static void Mutate(ref object value) { }
                static void Initialize(out object value) => value = new object();
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"),
            Diagnostic().WithLocation(3).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference"));
    }

    [Test]
    public async Task ReadOnly_ref_parameter_referent_writable_aliases_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref int p)
                {
                    Mutate(ref p);
                    Initialize(out p);
                    ref int alias = ref p;
                    int other = 0;
                    ref int reassigned = ref other;
                    reassigned = ref p;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_out_parameter_initialized_referent_writable_aliases_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] out int p)
                {
                    p = 0;
                    Mutate(ref p);
                    Initialize(out p);
                    ref int alias = ref p;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_in_parameter_readonly_aliases_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] in int p)
                {
                    Read(in p);
                    ref readonly int alias = ref p;
                }
                static void Read(in int value) { }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_readonly_parameter_readonly_aliases_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref readonly int p)
                {
                    Read(in p);
                    ref readonly int alias = ref p;
                }
                static void Read(ref readonly int value) { }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_referent_ref_return_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                ref int M([ReadOnly] ref int p) => ref p;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_in_parameter_referent_ref_readonly_return_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                ref readonly int M([ReadOnly] in int p)
                {
                    return ref p;
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_readonly_parameter_referent_ref_readonly_return_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                ref readonly int M([ReadOnly] ref readonly int p) => ref p;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_mutable_struct_field_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    Mutate(ref {|#1:p|}.MutableFieldInStruct);
                    Initialize(out {|#2:p|}.MutableFieldInStruct);
                    ref int alias = ref {|#3:p|}.MutableFieldInStruct;
                    ref readonly int readOnlyAlias = ref p.MutableFieldInStruct;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct' which is stored inline within 'p'"),
            Diagnostic().WithLocation(3).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_inline_array_element_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    Mutate(ref {|#1:p|}[0]);
                    Initialize(out {|#2:p|}[0]);
                    ref int alias = ref {|#3:p|}[0];
                    ref readonly int readOnlyAlias = ref p[0];
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0]' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0]' which is stored inline within 'p'"),
            Diagnostic().WithLocation(3).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_nested_mutable_struct_field_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref {|#1:p|}.MutableFieldInStruct.MutableFieldInStruct2);
                    ref int alias = ref {|#2:p|}.MutableFieldInStruct.MutableFieldInStruct2;
                    ref readonly int readOnlyAlias = ref p.MutableFieldInStruct.MutableFieldInStruct2;
                }
                static void Mutate(ref int value) { }
            }
            struct Outer { public Inner MutableFieldInStruct; }
            struct Inner { public int MutableFieldInStruct2; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct.MutableFieldInStruct2' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct.MutableFieldInStruct2' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_nested_inline_array_element_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] OuterBuffer p)
                {
                    Initialize(out {|#1:p|}[0][0]);
                    ref int alias = ref {|#2:p|}[0][0];
                    ref readonly int readOnlyAlias = ref p[0][0];
                }
                static void Initialize(out int value) => value = 1;
            }
            [InlineArray(2)]
            struct OuterBuffer { private InnerBuffer element; }
            [InlineArray(2)]
            struct InnerBuffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0][0]' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0][0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_inline_array_element_mutable_struct_field_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    Mutate(ref {|#1:p|}[0].MutableFieldInStruct);
                    ref int alias = ref {|#2:p|}[0].MutableFieldInStruct;
                    ref readonly int readOnlyAlias = ref p[0].MutableFieldInStruct;
                }
                static void Mutate(ref int value) { }
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_struct_inline_array_field_element_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] S p)
                {
                    Initialize(out {|#1:p|}.MutableFieldInStruct[0]);
                    ref int alias = ref {|#2:p|}.MutableFieldInStruct[0];
                    ref readonly int readOnlyAlias = ref p.MutableFieldInStruct[0];
                }
                static void Initialize(out int value) => value = 1;
            }
            struct S { public Buffer MutableFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct[0]' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_deep_struct_and_inline_array_writable_references()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref {|#1:p|}.MutableFieldInStruct[0].MutableFieldInStruct2[0][0].Value);
                    ref int alias = ref {|#2:p|}.MutableFieldInStruct[0].MutableFieldInStruct2[0][0].Value;
                    ref readonly int readOnlyAlias = ref p.MutableFieldInStruct[0].MutableFieldInStruct2[0][0].Value;
                }
                static void Mutate(ref int value) { }
            }
            struct Outer { public Buffer MutableFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private Inner element; }
            struct Inner { public OuterBuffer MutableFieldInStruct2; }
            [InlineArray(2)]
            struct OuterBuffer { private InnerBuffer element; }
            [InlineArray(2)]
            struct InnerBuffer { private Leaf element; }
            struct Leaf { public int Value; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct[0].MutableFieldInStruct2[0][0].Value' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct[0].MutableFieldInStruct2[0][0].Value' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_nested_storage_ref_local_and_field_reassignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    int other = 0;
                    ref int alias = ref other;
                    alias = ref {|#1:p|}[0].MutableFieldInStruct;
                    scoped Holder holder = default;
                    holder.Value = ref {|#2:p|}[0].MutableFieldInStruct;
                }
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            ref struct Holder { public ref int Value; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_nested_storage_readonly_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    Read(in p[0].MutableFieldInStruct);
                    ReadReference(ref p[0].MutableFieldInStruct);
                    ref readonly int alias = ref p[0].MutableFieldInStruct;
                    alias = ref p[1].MutableFieldInStruct;
                    scoped Holder holder = default;
                    holder.Value = ref p[0].MutableFieldInStruct;
                }
                static void Read(in int value) { }
                static void ReadReference(ref readonly int value) { }
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            ref struct Holder { public ref readonly int Value; }
            """);
    }

    [Test]
    public async Task ReadOnly_class_parameter_mutable_field_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Item p)
                {
                    Mutate(ref p.MutableFieldInClass);
                    Initialize(out p.MutableFieldInClass);
                    ref int alias = ref p.MutableFieldInClass;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            class Item { public int MutableFieldInClass; }
            """);
    }

    [Test]
    public async Task ReadOnly_struct_parameter_class_field_nested_struct_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref p.MutableFieldInStruct.MutableFieldInClass.MutableFieldInStruct2);
                    Initialize(out p.MutableFieldInStruct.MutableFieldInClass.MutableFieldInStruct2);
                    ref int alias = ref p.MutableFieldInStruct.MutableFieldInClass.MutableFieldInStruct2;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            struct Outer { public Item MutableFieldInStruct; }
            class Item { public Inner MutableFieldInClass; }
            struct Inner { public int MutableFieldInStruct2; }
            """);
    }

    [Test]
    public async Task ReadOnly_struct_parameter_class_field_nested_inline_array_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref p.MutableFieldInStruct.MutableFieldInClass[0]);
                    Initialize(out p.MutableFieldInStruct.MutableFieldInClass[0]);
                    ref int alias = ref p.MutableFieldInStruct.MutableFieldInClass[0];
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            struct Outer { public Item MutableFieldInStruct; }
            class Item { public Buffer MutableFieldInClass; }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """);
    }

    [Test]
    public async Task ReadOnly_inline_array_class_element_field_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    Mutate(ref p[0].MutableFieldInClass);
                    Initialize(out p[0].MutableFieldInClass);
                    ref int alias = ref p[0].MutableFieldInClass;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            [InlineArray(2)]
            struct Buffer { private Item element; }
            class Item { public int MutableFieldInClass; }
            """);
    }

    [Test]
    public async Task ReadOnly_inline_array_class_element_slot_writable_references_disallowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Buffer p)
                {
                    Mutate(ref {|#1:p|}[0]);
                    Initialize(out {|#2:p|}[0]);
                    ref Item alias = ref {|#3:p|}[0];
                }
                static void Mutate(ref Item value) { }
                static void Initialize(out Item value) => value = new Item();
            }
            [InlineArray(2)]
            struct Buffer { private Item element; }
            class Item { public int MutableFieldInClass; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0]' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0]' which is stored inline within 'p'"),
            Diagnostic().WithLocation(3).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_struct_class_field_slot_writable_reference_disallowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    ref Item alias = ref {|#1:p|}.MutableFieldInStruct;
                }
            }
            struct S { public Item MutableFieldInStruct; }
            class Item { }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_mutable_ref_field_referent_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    Mutate(ref p.MutableRefFieldInStruct);
                    Initialize(out p.MutableRefFieldInStruct);
                    ref int alias = ref p.MutableRefFieldInStruct;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            ref struct S { public ref int MutableRefFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_nested_struct_ref_field_referent_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref p.MutableFieldInStruct.MutableRefFieldInStruct);
                    Initialize(out p.MutableFieldInStruct.MutableRefFieldInStruct);
                    ref int alias = ref p.MutableFieldInStruct.MutableRefFieldInStruct;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            ref struct Outer { public Inner MutableFieldInStruct; }
            ref struct Inner { public ref int MutableRefFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_ref_field_struct_referent_nested_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref p.MutableRefFieldInStruct.MutableFieldInStruct);
                    Initialize(out p.MutableRefFieldInStruct.MutableFieldInStruct);
                    ref int alias = ref p.MutableRefFieldInStruct.MutableFieldInStruct;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            ref struct Outer { public ref Inner MutableRefFieldInStruct; }
            struct Inner { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_ref_field_inline_array_referent_nested_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    Mutate(ref p.MutableRefFieldInStruct[0].MutableFieldInStruct);
                    Initialize(out p.MutableRefFieldInStruct[0].MutableFieldInStruct);
                    ref int alias = ref p.MutableRefFieldInStruct[0].MutableFieldInStruct;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            ref struct Outer { public ref Buffer MutableRefFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private Inner element; }
            struct Inner { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_struct_parameter_own_inline_field_writable_reference_disallowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    ref int alias = ref {|#1:p|}.MutableFieldInStruct;
                    ref int referentAlias = ref p.MutableRefFieldInStruct;
                }
            }
            ref struct S
            {
                public int MutableFieldInStruct;
                public ref int MutableRefFieldInStruct;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by taking a writable reference to 'p.MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_class_parameter_mutable_field_ref_return_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                ref int M([ReadOnly] Item p) => ref p.MutableFieldInClass;
            }
            class Item { public int MutableFieldInClass; }
            """);
    }

    [Test]
    public async Task ReadOnly_out_parameter_initialized_object_field_ref_return_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                ref int M([ReadOnly] out Item p)
                {
                    p = new Item();
                    return ref p.MutableFieldInClass;
                }
            }
            class Item { public int MutableFieldInClass; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_mutable_ref_field_referent_ref_return_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                ref int M([ReadOnly] S p) => ref p.MutableRefFieldInStruct;
            }
            ref struct S { public ref int MutableRefFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_nested_inline_storage_writable_references_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] ref Buffer p)
                {
                    Mutate(ref p[0].MutableFieldInStruct);
                    Initialize(out p[0].MutableFieldInStruct);
                    ref int alias = ref p[0].MutableFieldInStruct;
                }
                static void Mutate(ref int value) { }
                static void Initialize(out int value) => value = 1;
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            """);
    }
}
