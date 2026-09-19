namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Diagnostics.CodeAnalysis;

public abstract class AnalyzerTests<TAnalyzer> : CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public sealed class TestConfig : TestConfigCore<TestConfig, TestConfig.Test>
    {
        public TestConfig() { }

        private TestConfig(Action<Test> setupActions) : base(setupActions) { }

        protected override TestConfig Create(Action<Test> setupActions) => new(setupActions);

        public async Task RunTestAsync([StringSyntax("C#-Test")] string source, params IEnumerable<DiagnosticResult> expectedDiagnostics)
        {
            var test = new Test();
            ApplySetupActions(test);

            test.TestState.Sources.Add(source);

            test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

            await RunTestAsync(test);
        }

        public sealed class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>, ITest
        {
            public LanguageVersion LanguageVersion { get; set; }

            protected override ParseOptions CreateParseOptions()
            {
                return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
            }
        }
    }
}

public abstract class AnalyzerTests<TAnalyzer, TCodeFixProvider> : CSharpCodeFixVerifier<TAnalyzer, TCodeFixProvider, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFixProvider : CodeFixProvider, new()
{
    public sealed class TestConfig : TestConfigCore<TestConfig, TestConfig.Test>
    {
        public TestConfig() { }

        private TestConfig(Action<Test> setupActions) : base(setupActions) { }

        protected override TestConfig Create(Action<Test> setupActions) => new(setupActions);

        public async Task RunTestAsync([StringSyntax("C#-Test")] string source, params IEnumerable<DiagnosticResult> expectedDiagnostics)
        {
            var test = new Test();
            ApplySetupActions(test);
            test.TestState.Sources.Add(source);
            test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

            // Since the test setup involves a code fix but no code fix is expected, the user intends to assert that no
            // fix is offered.
            test.FixedState.InheritanceMode = StateInheritanceMode.AutoInheritAll;
            test.NumberOfIncrementalIterations = 0;
            test.NumberOfFixAllIterations = 0;

            await RunTestAsync(test);
        }

        public async Task RunTestAsync([StringSyntax("C#-Test")] string source, [StringSyntax("C#-Test")] string fixedSource)
        {
            await RunTestAsync(source, [], fixedSource);
        }

        public async Task RunTestAsync([StringSyntax("C#-Test")] string source, DiagnosticResult expectedDiagnostic, [StringSyntax("C#-Test")] string fixedSource)
        {
            await RunTestAsync(source, [expectedDiagnostic], fixedSource);
        }

        public async Task RunTestAsync([StringSyntax("C#-Test")] string source, IEnumerable<DiagnosticResult> expectedDiagnostics, [StringSyntax("C#-Test")] string fixedSource)
        {
            var test = new Test();
            ApplySetupActions(test);

            test.FixedState.Sources.AddRange(test.TestState.Sources);
            test.TestCode = source;
            test.FixedCode = fixedSource;

            test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

            await RunTestAsync(test);
        }

        public sealed class Test : CSharpCodeFixTest<TAnalyzer, TCodeFixProvider, DefaultVerifier>, ITest
        {
            public LanguageVersion LanguageVersion { get; set; }

            protected override ParseOptions CreateParseOptions()
            {
                return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
            }
        }
    }
}
