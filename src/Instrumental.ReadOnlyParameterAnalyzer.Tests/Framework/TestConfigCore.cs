namespace Instrumental.ReadOnlyParameterAnalyzer.Tests.Framework;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using System.Diagnostics.CodeAnalysis;

public abstract class TestConfigCore<TSelf, TTest>()
    where TSelf : TestConfigCore<TSelf, TTest>
    where TTest : AnalyzerTest<DefaultVerifier>, TestConfigCore<TSelf, TTest>.ITest
{
    public interface ITest
    {
        LanguageVersion LanguageVersion { get; set; }
    }

    private readonly Action<TTest> setupActions;

    protected TestConfigCore(Action<TTest> setupActions) : this()
    {
        this.setupActions = setupActions;
    }

    protected abstract TSelf Create(Action<TTest> setupActions);

    protected TSelf With(Action<TTest> addedSetupActions)
    {
        return Create(setupActions + addedSetupActions);
    }

    public TSelf WithLanguageVersion(LanguageVersion languageVersion)
    {
        return With(test => test.LanguageVersion = languageVersion);
    }

    public TSelf AddSource(string fileName, [StringSyntax("C#-Test")] string source)
    {
        return With(test => test.TestState.Sources.Add((fileName, source)));
    }

    protected void ApplySetupActions(TTest test)
    {
        setupActions?.Invoke(test);
    }

    protected static async Task RunTestAsync(TTest test)
    {
        try
        {
            await test.RunAsync(TUnit.Core.TestContext.Current.Execution.CancellationToken);
        }
        catch (Exception ex) when (ex.Message.Replace("VerifyCS.", "") is var fixedMessage && !ReferenceEquals(ex.Message, fixedMessage))
        {
            throw (Exception)Activator.CreateInstance(ex.GetType(), fixedMessage, ex.InnerException);
        }
    }
}
