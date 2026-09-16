namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

internal static class TestSource
{
    public static string Code([StringSyntax("C#")] string source) => source;

    public static string Markup([StringSyntax("C#-Test")] string source) => source;

    public static SyntaxTree Parse([StringSyntax("C#")] string source, CSharpParseOptions options = null) =>
        CSharpSyntaxTree.ParseText(source, options);

    public static SourceText Text([StringSyntax("C#")] string source) => SourceText.From(source);
}
