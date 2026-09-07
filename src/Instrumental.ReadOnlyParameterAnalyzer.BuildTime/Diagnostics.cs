using Microsoft.CodeAnalysis;

namespace Instrumental.ReadOnlyParameterAnalyzer;

public static class Diagnostics
{
    public static class ReadOnlyParameterMutation
    {
        public static readonly DiagnosticDescriptor Descriptor = new(
            id: "IRP0001",
            title: "Possible mutation of readonly parameter",
            messageFormat: "Parameter '{0}' is marked as readonly via {1}, but it is possibly mutated by {2}",
            category: "Instrumental",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static class Properties
        {
            public const string ParameterName = "ParameterName";

            /// <summary>
            /// Hint that a defensive copy fix is available. This should be offered in scenarios for which the compiler
            /// would automatically create a defensive copy if the parameter were instead a readonly field.
            /// </summary>
            public const string DefensiveCopyFix = "DefensiveCopyFix";
        }
    }
}
