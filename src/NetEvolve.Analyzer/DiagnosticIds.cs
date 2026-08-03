namespace NetEvolve.Analyzer;

/// <summary>
/// Central registry of every diagnostic identifier shipped by this package. Identifiers use the
/// <see cref="Prefix">NE</see> prefix and are assigned sequentially; the owning
/// <see cref="DiagnosticCategories">category</see> is recorded on the descriptor and reflected by the
/// folder the rule lives in.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>The prefix shared by all NetEvolve diagnostic identifiers.</summary>
    public const string Prefix = "NE";

    /// <summary>
    /// Diagnostic identifiers are registered here as rules are added, each as a
    /// <c>public const string NExxxx</c> field grouped by its category.
    /// </summary>
    private const string HelpLinkBase = "https://github.com/dailydevops/analyzer/blob/main/docs/rules/";

    /// <summary>Builds the documentation help link for a diagnostic identifier.</summary>
    /// <param name="diagnosticId">The diagnostic identifier, e.g. <c>NE0001</c>.</param>
    /// <returns>An absolute URI pointing at the rule's documentation.</returns>
    public static string HelpLink(string diagnosticId) => $"{HelpLinkBase}{diagnosticId}.md";
}
