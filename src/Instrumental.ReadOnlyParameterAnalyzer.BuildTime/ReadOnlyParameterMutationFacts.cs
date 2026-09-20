using Microsoft.CodeAnalysis;

namespace Instrumental.ReadOnlyParameterAnalyzer;

internal static class ReadOnlyParameterMutationFacts
{
    public static bool IsReadOnlyParameterAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass is
        {
            Name: "ReadOnlyAttribute",
            ContainingSymbol: INamespaceSymbol
            {
                Name: "Annotations",
                ContainingNamespace:
                {
                    Name: "Instrumental",
                    ContainingNamespace.IsGlobalNamespace: true
                }
            }
        };
    }
}
