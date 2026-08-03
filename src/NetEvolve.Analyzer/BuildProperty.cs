namespace NetEvolve.Analyzer;

/// <summary>
/// Central registry of the MSBuild build-property keys that rules read through
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions"/>. Each value carries the
/// <c>build_property.</c> prefix that Roslyn uses for <c>CompilerVisibleProperty</c> values, so the keys can be
/// passed straight to <c>TryGetValue</c>. The matching <c>CompilerVisibleProperty</c> declarations ship in
/// <c>build/NetEvolve.Analyzer.props</c>.
/// </summary>
internal static class BuildProperty
{
    /// <summary>The prefix Roslyn applies to every compiler-visible MSBuild property.</summary>
    private const string Prefix = "build_property.";

    /// <summary>
    /// <c>PublishSingleFile</c> — set by the SDK for single-file publishes; disables the file-organization rules.
    /// </summary>
    public const string PublishSingleFile = Prefix + "PublishSingleFile";

    /// <summary>
    /// <c>NetEvolveAnalyzerDisableFileOrganizationRules</c> — explicit opt-out of the file-organization rules.
    /// </summary>
    public const string DisableFileOrganizationRules = Prefix + "NetEvolveAnalyzerDisableFileOrganizationRules";

    /// <summary>
    /// <c>NetEvolveAnalyzerGroupGenericOverloads</c> — allow generic overloads that share a base name to live in
    /// a single file named after the base identifier.
    /// </summary>
    public const string GroupGenericOverloads = Prefix + "NetEvolveAnalyzerGroupGenericOverloads";

    /// <summary>
    /// <c>RootNamespace</c> — the namespace anchor NE0002 uses as the root of the folder-derived namespace.
    /// </summary>
    public const string RootNamespace = Prefix + "RootNamespace";

    /// <summary>
    /// <c>ProjectDir</c> — the project directory NE0002 measures a file's folder path against.
    /// </summary>
    public const string ProjectDir = Prefix + "ProjectDir";
}
