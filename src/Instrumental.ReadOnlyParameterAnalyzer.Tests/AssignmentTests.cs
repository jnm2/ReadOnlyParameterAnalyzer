namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using TUnit.Core;

public class AssignmentTests : Framework.AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
        .WithReferenceAssemblies(ReferenceAssemblies.Net.Net100)
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                int M() => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    [Arguments("+=")]
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
    [Arguments("??=")]
    public async Task ReadOnly_primary_parameter_compound_assignment_in_method_body(string assignmentOperator)
    {
        await DefaultConfig.RunTestAsync($$"""
            using Instrumental.Annotations;
            class C([ReadOnly] int? p)
            {
                void M() => {|#1:p|} {{assignmentOperator}} 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                $"Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '{assignmentOperator}' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p, [ReadOnly] int p2)
            {
                void M() => ({|#1:p|}, ({|#2:p2|}, _)) = (5, (6, 7));
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p2' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameters_direct_and_inline_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p, [ReadOnly] int p2)
            {
                void M() => (({|#1:p|}[0].X, {|#2:p2|}), {|#3:p|}[1].X) = ((5, 6), 7);
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int X; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0].X' which is stored inline within 'p'"),
            Diagnostic().WithLocation(2).WithMessage(
                "Parameter 'p2' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"),
            Diagnostic().WithLocation(3).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[1].X' which is stored inline within 'p'"));
    }

    [Test]
    [Arguments("++")]
    [Arguments("--")]
    public async Task ReadOnly_primary_parameter_prefix_increment_or_decrement_in_method_body(string assignmentOperator)
    {
        await DefaultConfig.RunTestAsync($$"""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                int M() => {{assignmentOperator}}{|#1:p|};
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                $"Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '{assignmentOperator}' assignment"));
    }

    [Test]
    [Arguments("++")]
    [Arguments("--")]
    public async Task ReadOnly_primary_parameter_postfix_increment_or_decrement_in_method_body(string assignmentOperator)
    {
        await DefaultConfig.RunTestAsync($$"""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                int M() => {|#1:p|}{{assignmentOperator}};
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                $"Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '{assignmentOperator}' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M() => {|#1:p|}[0] = 5;
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_mutable_struct_field_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.MutableFieldInStruct = 5;
            }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p.MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_nested_inline_array_element_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] OuterBuffer p)
            {
                void M() => {|#1:p|}[0][0] = 5;
            }
            [InlineArray(2)]
            struct OuterBuffer { private InnerBuffer element; }
            [InlineArray(2)]
            struct InnerBuffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0][0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_mutable_struct_field_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M() => {|#1:p|}[0].MutableFieldInStruct = 5;
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_struct_inline_array_field_element_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.InlineArrayFieldInStruct[0] = 5;
            }
            struct S { public Buffer InlineArrayFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p.InlineArrayFieldInStruct[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_nested_mutable_struct_field_simple_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.MutableFieldInStruct.MutableFieldInStruct2 = 5;
            }
            struct S { public S2 MutableFieldInStruct; }
            struct S2 { public int MutableFieldInStruct2; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p.MutableFieldInStruct.MutableFieldInStruct2' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M() => ({|#1:p|}[0], _) = (5, 6);
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_mutable_struct_field_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => ({|#1:p|}.MutableFieldInStruct, _) = (5, 6);
            }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p.MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_nested_inline_array_element_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] OuterBuffer p)
            {
                void M() => ({|#1:p|}[0][0], _) = (5, 6);
            }
            [InlineArray(2)]
            struct OuterBuffer { private InnerBuffer element; }
            [InlineArray(2)]
            struct InnerBuffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0][0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_mutable_struct_field_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M() => ({|#1:p|}[0].MutableFieldInStruct, _) = (5, 6);
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_struct_inline_array_field_element_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] S p)
            {
                void M() => ({|#1:p|}.InlineArrayFieldInStruct[0], _) = (5, 6);
            }
            struct S { public Buffer InlineArrayFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p.InlineArrayFieldInStruct[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_nested_mutable_struct_field_deconstructing_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => ({|#1:p|}.MutableFieldInStruct.MutableFieldInStruct2, _) = (5, 6);
            }
            struct S { public S2 MutableFieldInStruct; }
            struct S2 { public int MutableFieldInStruct2; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment to 'p.MutableFieldInStruct.MutableFieldInStruct2' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_postfix_increment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M() => {|#1:p|}[0]++;
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '++' assignment to 'p[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_mutable_struct_field_postfix_increment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.MutableFieldInStruct++;
            }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '++' assignment to 'p.MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_nested_inline_array_element_postfix_increment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] OuterBuffer p)
            {
                void M() => {|#1:p|}[0][0]++;
            }
            [InlineArray(2)]
            struct OuterBuffer { private InnerBuffer element; }
            [InlineArray(2)]
            struct InnerBuffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '++' assignment to 'p[0][0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_mutable_struct_field_postfix_increment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M() => {|#1:p|}[0].MutableFieldInStruct++;
            }
            [InlineArray(2)]
            struct Buffer { private S element; }
            struct S { public int MutableFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '++' assignment to 'p[0].MutableFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_struct_inline_array_field_element_postfix_increment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.InlineArrayFieldInStruct[0]++;
            }
            struct S { public Buffer InlineArrayFieldInStruct; }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '++' assignment to 'p.InlineArrayFieldInStruct[0]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_nested_mutable_struct_field_postfix_increment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M() => {|#1:p|}.MutableFieldInStruct.MutableFieldInStruct2++;
            }
            struct S { public S2 MutableFieldInStruct; }
            struct S2 { public int MutableFieldInStruct2; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '++' assignment to 'p.MutableFieldInStruct.MutableFieldInStruct2' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_compound_assignment_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p, [ReadOnly] int index, [ReadOnly] int value)
            {
                void M() => {|#1:p|}[index] += value;
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '+=' assignment to 'p[index]' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_class_struct_field_assignments_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] C2 p)
            {
                void M()
                {
                    p.StructField.MutableFieldInStruct = 5;
                    (p.StructField.MutableFieldInStruct, _) = (5, 6);
                    p.StructField.MutableFieldInStruct++;
                }
            }
            class C2 { public S StructField; }
            struct S { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_struct_class_field_assignments_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p)
            {
                void M()
                {
                    p.ClassField.MutableFieldInClass = 5;
                    (p.ClassField.MutableFieldInClass, _) = (5, 6);
                    p.ClassField.MutableFieldInClass++;
                }
            }
            struct S { public C2 ClassField; }
            class C2 { public int MutableFieldInClass; }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_inline_array_element_class_field_assignments_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C([ReadOnly] Buffer p)
            {
                void M()
                {
                    p[0].MutableFieldInClass = 5;
                    (p[0].MutableFieldInClass, _) = (5, 6);
                    p[0].MutableFieldInClass++;
                }
            }
            [InlineArray(2)]
            struct Buffer { private C2 element; }
            class C2 { public int MutableFieldInClass; }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_struct_array_field_element_assignments_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] S p, [ReadOnly] int index, [ReadOnly] int value)
            {
                void M()
                {
                    p.ArrayField[index].MutableFieldInStruct = value;
                    (p.ArrayField[index].MutableFieldInStruct, _) = (value, 6);
                    p.ArrayField[index].MutableFieldInStruct++;
                }
            }
            struct S { public S2[] ArrayField; }
            struct S2 { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_regular_parameter_struct_ref_field_assignments_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    p.RefField.MutableFieldInStruct = 5;
                    (p.RefField.MutableFieldInStruct, _) = (5, 6);
                    p.RefField.MutableFieldInStruct++;
                }
            }
            ref struct S { public ref S2 RefField; }
            struct S2 { public int MutableFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_field_initializer()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                int f = {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_property_initializer()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                int P { get; set; } = {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_event_initializer()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] System.Action p)
            {
                event System.Action E = {|#1:p|} = () => { };
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_primary_constructor_chain_call()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class B(int p);
            class C([ReadOnly] int p) : B({|#1:p|} = 5);
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_regular_constructor_parameter_simple_assignment_in_constructor_chain_call()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public C([ReadOnly] int p) : this({|#1:p|} = 5, false) { }
                public C(int p, bool p2) { }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_lambda_in_method_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                System.Action M() => () => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_primary_parameter_simple_assignment_in_lambda_in_field_initializer()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p)
            {
                System.Action f = () => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_indexer_parameter_simple_assignment_in_getter()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int this[[ReadOnly] int p]
                {
                    get => {|#1:p|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_indexer_parameter_simple_assignment_in_setter()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int this[[ReadOnly] int p]
                {
                    set => {|#1:p|} = value;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_regular_method_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p) => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_regular_constructor_parameter_simple_assignment_in_body()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public C([ReadOnly] int p) => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_local_function_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M()
                {
                    void Local([ReadOnly] int p) => {|#1:p|} = 5;
                    Local(0);
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_lambda_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                System.Action<int> M() => ([ReadOnly] int p) => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_unary_operator_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static C operator -([ReadOnly] C p) => {|#1:p|} = new C();
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_binary_operator_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static C operator +([ReadOnly] C p, C other) => {|#1:p|} = other;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_implicit_conversion_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static implicit operator C([ReadOnly] int p)
                {
                    {|#1:p|} = 5;
                    return new C();
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_explicit_conversion_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static explicit operator C([ReadOnly] int p)
                {
                    {|#1:p|} = 5;
                    return new C();
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_extension_method_receiver_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            static class C
            {
                public static void M([ReadOnly] this int p) => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_extension_block_receiver_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            static class C
            {
                extension([ReadOnly] int p)
                {
                    public void M() => {|#1:p|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_extension_block_method_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            static class C
            {
                extension(int receiver)
                {
                    public void M([ReadOnly] int p) => {|#1:p|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_property_setter_value_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int P
                {
                    [param: ReadOnly]
                    set => {|#1:value|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_property_init_value_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int P
                {
                    [param: ReadOnly]
                    init => {|#1:value|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_indexer_setter_value_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int this[int index]
                {
                    [param: ReadOnly]
                    set => {|#1:value|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_indexer_init_value_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int this[int index]
                {
                    [param: ReadOnly]
                    init => {|#1:value|} = 5;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_event_add_value_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                event System.Action E
                {
                    [param: ReadOnly]
                    add => {|#1:value|} = null;
                    remove { }
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_event_remove_value_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                event System.Action E
                {
                    add { }
                    [param: ReadOnly]
                    remove => {|#1:value|} = null;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_struct_primary_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            struct S([ReadOnly] int p)
            {
                void M() => {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_record_class_primary_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            record C([ReadOnly] int p)
            {
                private int field = {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_record_struct_primary_parameter_simple_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            record struct S([ReadOnly] int p)
            {
                private int field = {|#1:p|} = 5;
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '=' assignment"));
    }

    [Test]
    public async Task ReadOnly_ref_parameter_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref int p, ref int other)
                {
                    {|#1:p|} = ref other;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment"));
    }

    [Test]
    public async Task ReadOnly_out_parameter_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] out int p, ref int other)
                {
                    p = 0;
                    {|#1:p|} = ref other;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment"));
    }

    [Test]
    public async Task ReadOnly_ref_readonly_parameter_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref readonly int p, ref int other)
                {
                    {|#1:p|} = ref other;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment"));
    }

    [Test]
    public async Task ReadOnly_in_parameter_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] in int p, ref int other)
                {
                    {|#1:p|} = ref other;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment"));
    }

    [Test]
    public async Task ReadOnly_ref_parameter_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref int p)
                {
                    p = 5;
                    p += 1;
                    p++;
                    --p;
                    (p, _) = (6, 7);
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_out_parameter_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] out int p)
                {
                    p = 5;
                    p += 1;
                    p++;
                    --p;
                    (p, _) = (6, 7);
                }
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_null_coalescing_assignment_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] ref string p) => p ??= "";
            }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_inline_array_element_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            using System.Runtime.CompilerServices;
            class C
            {
                void M([ReadOnly] ref Buffer p)
                {
                    p[0] = 5;
                    p[0]++;
                    (p[0], _) = (6, 7);
                }
            }
            [InlineArray(2)]
            struct Buffer { private int element; }
            """);
    }

    [Test]
    public async Task ReadOnly_ref_parameter_nested_field_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                static int other;
                void M([ReadOnly] ref Outer p)
                {
                    p.MutableFieldInStruct.MutableRefFieldInStruct = ref other;
                    p.MutableFieldInStruct.MutableRefFieldInStruct = 5;
                    p.MutableFieldInStruct.Value = 6;
                }
            }
            ref struct Outer { public Inner MutableFieldInStruct; }
            ref struct Inner
            {
                public ref int MutableRefFieldInStruct;
                public int Value;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_out_parameter_nested_field_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                static int other;
                void M([ReadOnly] out Outer p)
                {
                    p = default;
                    p.MutableFieldInStruct.MutableRefFieldInStruct = ref other;
                    p.MutableFieldInStruct.MutableRefFieldInStruct = 5;
                    p.MutableFieldInStruct.Value = 6;
                }
            }
            ref struct Outer { public Inner MutableFieldInStruct; }
            ref struct Inner
            {
                public ref int MutableRefFieldInStruct;
                public int Value;
            }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_ref_field_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                static int other;
                void M([ReadOnly] S p)
                {
                    {|#1:p|}.MutableRefFieldInStruct = ref other;
                }
            }
            ref struct S { public ref int MutableRefFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment to 'p.MutableRefFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_ref_readonly_field_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                static int other;
                void M([ReadOnly] S p)
                {
                    {|#1:p|}.MutableRefFieldInStruct = ref other;
                }
            }
            ref struct S { public ref readonly int MutableRefFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment to 'p.MutableRefFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_ref_field_value_assignments_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p)
                {
                    p.MutableRefFieldInStruct = 5;
                    p.MutableRefFieldInStruct += 1;
                    p.MutableRefFieldInStruct++;
                    --p.MutableRefFieldInStruct;
                    (p.MutableRefFieldInStruct, _) = (6, 7);
                }
            }
            ref struct S { public ref int MutableRefFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_ref_field_null_coalescing_assignment_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] S p) => p.MutableRefFieldInStruct ??= "";
            }
            ref struct S { public ref string MutableRefFieldInStruct; }
            """);
    }

    [Test]
    public async Task ReadOnly_parameter_nested_inline_ref_field_ref_assignment()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                static int other;
                void M([ReadOnly] Outer p)
                {
                    {|#1:p|}.MutableFieldInStruct.MutableRefFieldInStruct = ref other;
                }
            }
            ref struct Outer { public Inner MutableFieldInStruct; }
            ref struct Inner { public ref int MutableRefFieldInStruct; }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' is marked as readonly via [ReadOnly] on the parameter declaration, but it is possibly mutated by '= ref' assignment to 'p.MutableFieldInStruct.MutableRefFieldInStruct' which is stored inline within 'p'"));
    }

    [Test]
    public async Task ReadOnly_parameter_nested_inline_ref_field_value_assignment_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] Outer p)
                {
                    p.MutableFieldInStruct.MutableRefFieldInStruct = 5;
                }
            }
            ref struct Outer { public Inner MutableFieldInStruct; }
            ref struct Inner { public ref int MutableRefFieldInStruct; }
            """);
    }
}
