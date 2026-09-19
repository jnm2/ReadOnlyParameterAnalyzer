namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using System.Threading.Tasks;
using TUnit.Core;

public class AssignmentTests : Framework.AnalyzerTests<ReadOnlyParameterMutationAnalyzer, ReadOnlyParameterMutationCodeFixProvider>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
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
