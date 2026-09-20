namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis.Testing;
using TUnit.Core;

public class DeclarationTests : Framework.AnalyzerTests<ReadOnlyParameterDeclarationAnalyzer>
{
    private static readonly TestConfig DefaultConfig = new TestConfig()
        .WithReferenceAssemblies(ReferenceAssemblies.Net.Net100)
        .AddSource("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource);

    [Test]
    public async Task Delegate_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            delegate void D([{|#1:ReadOnly|}] int p);
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Extern_method_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static extern void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Extern_constructor_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public extern C([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Extern_operator_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static extern C operator +([{|#1:ReadOnly|}] C p, int other);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Extern_conversion_operator_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static extern explicit operator int([{|#1:ReadOnly|}] C p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Extern_indexer_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public extern int this[[{|#1:ReadOnly|}] int p] { get; set; }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Abstract_method_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public abstract void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Abstract_indexer_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public abstract int this[[{|#1:ReadOnly|}] int p] { get; set; }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Interface_method_parameter_without_body_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Interface_indexer_parameter_without_body_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int this[[{|#1:ReadOnly|}] int p] { get; set; }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Static_abstract_interface_method_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                static abstract void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Static_abstract_interface_operator_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I<T> where T : I<T>
            {
                static abstract T operator +([{|#1:ReadOnly|}] T p, T other);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Static_abstract_interface_conversion_operator_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I<T> where T : I<T>
            {
                static abstract explicit operator int([{|#1:ReadOnly|}] T p);
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Extern_local_function_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M()
                {
                    [System.Runtime.InteropServices.DllImport("native")]
                    static extern void Local([{|#1:ReadOnly|}] int p);
                }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'p' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Attribute_without_arguments_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            delegate void D([{|#1:ReadOnly|}] int p);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Attribute_with_true_argument_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            delegate void D([{|#1:ReadOnly(true)|}] int p);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Attribute_with_false_argument_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            delegate void D([{|#1:ReadOnly(false)|}] int p);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Qualified_attribute_name_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            delegate void D([{|#1:Instrumental.Annotations.ReadOnlyAttribute|}] int p);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Globally_qualified_attribute_name_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            delegate void D([{|#1:global::Instrumental.Annotations.ReadOnly|}] int p);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Aliased_attribute_name_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Alias = Instrumental.Annotations.ReadOnlyAttribute;
            delegate void D([{|#1:Alias|}] int p);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Each_attribute_is_reported_once()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            delegate void D([{|#1:ReadOnly|}] int p, [{|#2:ReadOnly|}] int q);
            """,
            Diagnostic().WithLocation(1).WithArguments("p"),
            Diagnostic().WithLocation(2).WithArguments("q"));
    }

    [Test]
    public async Task Partial_method_parameter_without_implementation_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                partial void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Partial_method_definition_parameter_with_implementation_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                partial void M([{|#1:ReadOnly|}] int p);
                partial void M(int p) { }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Partial_method_implementation_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                partial void M(int p);
                partial void M([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Partial_method_definition_parameter_with_implementation_in_another_file_is_blocked()
    {
        await DefaultConfig.AddSource("Implementation.cs", """
            partial class C
            {
                partial void M(int p) => System.Console.WriteLine(p);
            }
            """).RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                partial void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Partial_method_expression_body_implementation_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                partial void M(int p);
                partial void M([ReadOnly] int p) => System.Console.WriteLine(p);
            }
            """);
    }

    [Test]
    public async Task Partial_method_definition_parameter_with_extern_implementation_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                private partial void M([{|#1:ReadOnly|}] int p);
                private extern partial void M(int p);
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Extern_partial_method_implementation_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                private partial void M(int p);
                private extern partial void M([{|#1:ReadOnly|}] int p);
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Partial_indexer_definition_parameter_with_extern_implementation_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int this[[{|#1:ReadOnly|}] int p] { get; }
                public extern partial int this[int p] { get; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Extern_partial_indexer_implementation_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int this[int p] { get; }
                public extern partial int this[[{|#1:ReadOnly|}] int p] { get; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Method_parameter_with_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Method_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                int M([ReadOnly] int p) => p;
            }
            """);
    }

    [Test]
    public async Task Virtual_method_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public virtual void M([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Constructor_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public C([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Constructor_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public C([ReadOnly] int p) => System.Console.WriteLine(p);
            }
            """);
    }

    [Test]
    public async Task Constructor_parameter_with_this_initializer_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public C([ReadOnly] int p) : this(p, 0) { }
                private C(int p, int other) { }
            }
            """);
    }

    [Test]
    public async Task Constructor_parameter_with_base_initializer_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class B
            {
                protected B(int p) { }
            }
            class C : B
            {
                public C([ReadOnly] int p) : base(p) { }
            }
            """);
    }

    [Test]
    public async Task Constructor_parameter_with_initializer_but_no_body_is_allowed()
    {
        // Check initializer syntax independently of the compiler's current requirement for a constructor body.
        var test = new TestConfig.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = """
                using Instrumental.Annotations;
                class B
                {
                    protected B(int p) { }
                }
                class C : B
                {
                    public C([ReadOnly] int p) : base(p);
                }
                """
        };
        test.TestState.Sources.Add(("Instrumental.Annotations.ReadOnlyAttribute.g.cs", Resources.ReadOnlyAttributeSource));

        await test.RunAsync(TestContext.Current.Execution.CancellationToken);
    }

    [Test]
    public async Task Primary_constructor_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C([ReadOnly] int p);
            """);
    }

    [Test]
    public async Task Struct_primary_constructor_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            struct C([ReadOnly] int p);
            """);
    }

    [Test]
    public async Task Record_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            record C([ReadOnly] int p);
            """);
    }

    [Test]
    public async Task Record_struct_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            record struct C([ReadOnly] int p);
            """);
    }

    [Test]
    public async Task Indexer_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int this[[ReadOnly] int p] => p;
            }
            """);
    }

    [Test]
    public async Task Indexer_parameter_with_getter_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int this[[ReadOnly] int p]
                {
                    get { return p; }
                }
            }
            """);
    }

    [Test]
    public async Task Indexer_parameter_with_getter_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int this[[ReadOnly] int p]
                {
                    get => p;
                }
            }
            """);
    }

    [Test]
    public async Task Indexer_parameter_with_setter_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int this[[ReadOnly] int p]
                {
                    set { System.Console.WriteLine(p); }
                }
            }
            """);
    }

    [Test]
    public async Task Indexer_parameter_with_setter_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int this[[ReadOnly] int p]
                {
                    set => System.Console.WriteLine(p);
                }
            }
            """);
    }

    [Test]
    public async Task Operator_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static C operator +([ReadOnly] C p, int other) => p;
            }
            """);
    }

    [Test]
    public async Task Conversion_operator_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public static explicit operator int([ReadOnly] C p) => 0;
            }
            """);
    }

    [Test]
    public async Task Local_function_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M()
                {
                    void Local([ReadOnly] int p) { }
                }
            }
            """);
    }

    [Test]
    public async Task Local_function_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                void M()
                {
                    int Local([ReadOnly] int p) => p;
                }
            }
            """);
    }

    [Test]
    public async Task Lambda_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                System.Action<int> a = ([ReadOnly] int p) => { };
            }
            """);
    }

    [Test]
    public async Task Lambda_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                System.Func<int, int> f = ([ReadOnly] int p) => p;
            }
            """);
    }

    [Test]
    public async Task Interface_method_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                void M([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Static_virtual_interface_method_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                static virtual void M([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Static_sealed_interface_method_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                static sealed void M([ReadOnly] int p) { }
            }
            """);
    }

    [Test]
    public async Task Interface_method_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int M([ReadOnly] int p) => p;
            }
            """);
    }

    [Test]
    public async Task Interface_indexer_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int this[[ReadOnly] int p] => p;
            }
            """);
    }

    [Test]
    public async Task Partial_indexer_definition_parameter_with_implementation_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int this[[{|#1:ReadOnly|}] int p] { get; }
                public partial int this[int p] => p;
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"));
    }

    [Test]
    public async Task Partial_indexer_expression_body_implementation_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int this[int p] { get; }
                public partial int this[[ReadOnly] int p] => p;
            }
            """);
    }

    [Test]
    public async Task Partial_indexer_accessor_body_implementation_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int this[int p] { get; set; }
                public partial int this[[ReadOnly] int p]
                {
                    get { return p; }
                    set => System.Console.WriteLine(p);
                }
            }
            """);
    }

    [Test]
    public async Task Auto_property_setter_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int P { get; [param: {|#1:ReadOnly|}] set; }
            }
            """,
            Diagnostic().WithLocation(1).WithMessage(
                "Parameter 'value' cannot be marked with [ReadOnly] because its containing declaration does not have an executable body to guard against mutations"));
    }

    [Test]
    public async Task Auto_property_init_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int P { get; [param: {|#1:ReadOnly|}] init; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Abstract_property_setter_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public abstract int P { get; [param: {|#1:ReadOnly|}] set; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Abstract_property_init_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public abstract int P { get; [param: {|#1:ReadOnly|}] init; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Extern_property_setter_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public extern int P { get; [param: {|#1:ReadOnly|}] set; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Extern_property_init_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public extern int P { get; [param: {|#1:ReadOnly|}] init; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Interface_property_setter_parameter_without_body_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int P { get; [param: {|#1:ReadOnly|}] set; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Interface_property_init_parameter_without_body_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int P { get; [param: {|#1:ReadOnly|}] init; }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Indexer_parameter_and_bodyless_setter_parameter_are_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public abstract int this[[{|#1:ReadOnly|}] int p]
                {
                    get;
                    [param: {|#2:ReadOnly|}] set;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("p"),
            Diagnostic().WithLocation(2).WithArguments("value"));
    }

    [Test]
    public async Task Indexer_bodyless_init_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            abstract class C
            {
                public abstract int this[int p]
                {
                    get;
                    [param: {|#1:ReadOnly|}] init;
                }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Partial_property_definition_setter_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int P { get; [param: {|#1:ReadOnly|}] set; }
                public partial int P { get => 0; set { } }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Partial_property_definition_init_parameter_is_blocked()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int P { get; [param: {|#1:ReadOnly|}] init; }
                public partial int P { get => 0; init { } }
            }
            """,
            Diagnostic().WithLocation(1).WithArguments("value"));
    }

    [Test]
    public async Task Partial_property_implementation_setter_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int P { get; set; }
                public partial int P { get => 0; [param: ReadOnly] set { } }
            }
            """);
    }

    [Test]
    public async Task Partial_property_implementation_init_parameter_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            partial class C
            {
                public partial int P { get; init; }
                public partial int P { get => 0; [param: ReadOnly] init { } }
            }
            """);
    }

    [Test]
    public async Task Setter_parameter_with_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int P { get => 0; [param: ReadOnly] set { } }
            }
            """);
    }

    [Test]
    public async Task Setter_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int P { get => 0; [param: ReadOnly] set => System.Console.WriteLine(value); }
            }
            """);
    }

    [Test]
    public async Task Init_parameter_with_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int P { get => 0; [param: ReadOnly] init { } }
            }
            """);
    }

    [Test]
    public async Task Init_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public int P { get => 0; [param: ReadOnly] init => System.Console.WriteLine(value); }
            }
            """);
    }

    [Test]
    public async Task Interface_indexer_setter_parameter_with_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int this[[ReadOnly] int p]
                {
                    get => p;
                    [param: ReadOnly] set { }
                }
            }
            """);
    }

    [Test]
    public async Task Interface_indexer_setter_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int this[[ReadOnly] int p]
                {
                    get => p;
                    [param: ReadOnly] set => System.Console.WriteLine(value);
                }
            }
            """);
    }

    [Test]
    public async Task Interface_property_init_parameter_with_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            interface I
            {
                int P { get => 0; [param: ReadOnly] init { } }
            }
            """);
    }

    [Test]
    public async Task Event_add_parameter_with_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public event System.Action Event
                {
                    [param: ReadOnly] add { }
                    remove { }
                }
            }
            """);
    }

    [Test]
    public async Task Event_add_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public event System.Action Event
                {
                    [param: ReadOnly] add => System.Console.WriteLine(value);
                    remove { }
                }
            }
            """);
    }

    [Test]
    public async Task Event_remove_parameter_with_block_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public event System.Action Event
                {
                    add { }
                    [param: ReadOnly] remove { }
                }
            }
            """);
    }

    [Test]
    public async Task Event_remove_parameter_with_expression_body_is_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            using Instrumental.Annotations;
            class C
            {
                public event System.Action Event
                {
                    add { }
                    [param: ReadOnly] remove => System.Console.WriteLine(value);
                }
            }
            """);
    }

    [Test]
    public async Task Unrelated_attributes_are_allowed()
    {
        await new TestConfig()
            .WithReferenceAssemblies(ReferenceAssemblies.Net.Net100)
            .RunTestAsync("""
                class ReadOnlyAttribute : System.Attribute { }
                delegate void D([ReadOnly] int p);
                abstract class C { public abstract void M([ReadOnly] int p); }
                """);
    }

    [Test]
    public async Task Unrelated_attributes_are_allowed_when_ReadOnly_attribute_is_present_but_not_imported()
    {
        await DefaultConfig.RunTestAsync("""
            class ReadOnlyAttribute : System.Attribute { }
            delegate void D([ReadOnly] int p);
            abstract class C { public abstract void M([ReadOnly] int p); }
            """);
    }

    [Test]
    public async Task Unrelated_accessor_parameter_attributes_are_allowed()
    {
        await DefaultConfig.RunTestAsync("""
            class ReadOnlyAttribute : System.Attribute { }
            class C
            {
                public int P { get; [param: ReadOnly] set; }
                public int Q { get; [param: ReadOnly] init; }
            }
            """);
    }
}
