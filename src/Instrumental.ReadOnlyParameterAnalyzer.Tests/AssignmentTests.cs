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
}
