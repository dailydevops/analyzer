namespace NetEvolve.Analyzer;

/// <summary>
/// Central registry of every diagnostic identifier shipped by this package. Identifiers use the
/// <see cref="DiagnosticPrefix">NE</see> prefix and are assigned sequentially; the owning
/// <see cref="DiagnosticCategories">category</see> is recorded on the descriptor and reflected by the
/// folder the rule lives in.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>The prefix shared by all NetEvolve diagnostic identifiers.</summary>
    public const string DiagnosticPrefix = "NE";

    /// <summary>
    /// The prefix shared by all NetEvolve diagnostic suppressor identifiers. Suppressors are a special kind of analyzer.
    /// </summary>
    public const string SuppressionPrefix = "NES";

    /// <summary>
    /// Diagnostic identifiers are registered here as rules are added, each as a
    /// <c>public const string NExxxx</c> field grouped by its category.
    /// </summary>
    private const string HelpLinkBase = "https://github.com/dailydevops/analyzer/blob/main/docs/rules/";

    // Maintainability

    /// <summary>
    /// NE0001 — each file should declare a single top-level type whose name matches the file name.
    /// </summary>
    public const string NE0001 = DiagnosticPrefix + "0001";

    /// <summary>
    /// NE0002 — the declared namespace should match the folder structure, anchored at <c>RootNamespace</c>.
    /// </summary>
    public const string NE0002 = DiagnosticPrefix + "0002";

    /// <summary>
    /// NE0003 — a file should declare exactly one namespace.
    /// </summary>
    public const string NE0003 = DiagnosticPrefix + "0003";

    // Usage

    /// <summary>
    /// NE0004 — prefer the <c>is null</c> pattern over <c>== null</c> / <c>null ==</c>.
    /// </summary>
    public const string NE0004 = DiagnosticPrefix + "0004";

    /// <summary>
    /// NE0005 — prefer the <c>is not null</c> pattern over <c>!= null</c> / <c>null !=</c>.
    /// </summary>
    public const string NE0005 = DiagnosticPrefix + "0005";

    /// <summary>
    /// NE0006 — prefer the <c>is not null</c> pattern over an <c>is object</c> null check.
    /// </summary>
    public const string NE0006 = DiagnosticPrefix + "0006";

    // Documentation

    /// <summary>
    /// NE0007 — prefer <c>&lt;see langword="..."/&gt;</c> over <c>&lt;c&gt;...&lt;/c&gt;</c> for C# keywords.
    /// </summary>
    public const string NE0007 = DiagnosticPrefix + "0007";

    /// <summary>
    /// NE0008 — prefer <c>&lt;see cref="..."/&gt;</c> over <c>&lt;c&gt;...&lt;/c&gt;</c> for native type names.
    /// </summary>
    public const string NE0008 = DiagnosticPrefix + "0008";

    /// <summary>
    /// NE0009 — a method with a <see cref="System.Threading.CancellationToken"/> parameter should check for
    /// cancellation at the start of its body.
    /// </summary>
    public const string NE0009 = DiagnosticPrefix + "0009";

    /// <summary>
    /// NE0010 — a method returning <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c>,
    /// or <c>IAsyncEnumerable&lt;T&gt;</c> should accept a <c>CancellationToken</c> parameter.
    /// </summary>
    public const string NE0010 = DiagnosticPrefix + "0010";

    // Style

    /// <summary>
    /// NE0011 — avoid <c>#region</c>/<c>#endregion</c> directives.
    /// </summary>
    public const string NE0011 = DiagnosticPrefix + "0011";

    // Suppressors

    /// <summary>
    /// NES0001 — suppresses Meziantou.Analyzer's <c>MA0154</c> ("Use langword in XML comment") wherever
    /// NE0007 already reports the same location, avoiding a duplicate diagnostic when both analyzers run
    /// against the same compilation.
    /// </summary>
    public const string NES0001 = SuppressionPrefix + "0001";

    /// <summary>Builds the documentation help link for a diagnostic identifier.</summary>
    /// <param name="diagnosticId">The diagnostic identifier, e.g. <c>NE0001</c>.</param>
    /// <param name="category">The rule's <see cref="DiagnosticCategories">category</see>.</param>
    /// <returns>An absolute URI pointing at the rule's documentation, entirely lowercase.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "The documentation folder structure under docs/rules/ is intentionally lowercase; this is not a security-sensitive normalization."
    )]
    public static string HelpLink(string diagnosticId, string category) =>
        $"{HelpLinkBase}{category.ToLowerInvariant()}/{diagnosticId.ToLowerInvariant()}.md";
}
