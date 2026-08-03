namespace NetEvolve.Analyzer.Usage;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Resolves the effective C# <see cref="LanguageVersion"/> of the document being fixed so the null-check code
/// fixes (NE0004–NE0006) never emit a pattern the consuming project cannot compile. The analyzer assembly
/// targets <c>netstandard2.0</c> and loads for any consuming target framework (including <c>net48</c>), so a
/// project pinned to an older <c>LangVersion</c> must receive the diagnostic but only a fix its language version
/// supports — <c>is null</c> needs C# 7.0, <c>is not null</c> needs C# 9.0.
/// </summary>
internal static class LanguageVersionGate
{
    /// <summary>The effective language version of <paramref name="document"/>, mapping <c>Latest</c>/<c>Default</c>/<c>Preview</c> to a concrete version.</summary>
    public static LanguageVersion Effective(Document document) =>
        document.Project.ParseOptions is CSharpParseOptions parseOptions
            ? parseOptions.LanguageVersion.MapSpecifiedToEffectiveVersion()
            : LanguageVersion.Latest.MapSpecifiedToEffectiveVersion();

    /// <summary>Whether <paramref name="document"/> can compile the given <paramref name="required"/> language version.</summary>
    public static bool Supports(Document document, LanguageVersion required) => Effective(document) >= required;
}
