namespace NetEvolve.Analyzer;

using Microsoft.CodeAnalysis;

/// <summary>
/// Central registry of every <see cref="DiagnosticDescriptor"/> shipped by this package, one field per rule.
/// Descriptors reference their <see cref="DiagnosticIds">identifier</see> and
/// <see cref="DiagnosticCategories">category</see>, so the identifier, category and folder stay in lockstep.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>NE0001 — declare one top-level type per file, with a matching file name.</summary>
    public static readonly DiagnosticDescriptor OneTypePerFile = new(
        id: DiagnosticIds.NE0001,
        title: "Declare one type per file with a matching file name",
        messageFormat: "Type '{0}' should be declared in its own file named '{1}.cs'",
        category: DiagnosticCategories.Maintainability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each top-level type should live in its own file whose name matches the type. Generic "
            + "overloads are encoded by arity unless overload grouping is enabled.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0001)
    );
}
