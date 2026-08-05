namespace NetEvolve.Analyzer.Helpers;

using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Whether a <see cref="CSharpKeywords.ConditionalBclTypeMinimumVersions"/> type name (currently
/// <c>DateOnly</c>, <c>TimeOnly</c>) is safe to reference given a project's full multi-targeting list. Shared
/// by any rule that, like NE0008's code fix, is about to write a reference to one of these types and needs to
/// know whether every target framework the project builds for actually has it — not just the one currently
/// compiling.
/// </summary>
internal static class ConditionalBclTypeAvailability
{
    /// <summary>
    /// <see langword="true"/> when <paramref name="typeName"/> is a conditionally-available BCL type
    /// (<see cref="CSharpKeywords.ConditionalBclTypeMinimumVersions"/>) and the project's
    /// <see cref="BuildProperty.TargetFrameworks"/> list names at least one target framework older than the
    /// type's own minimum version. Types outside that dictionary are always safe (<see langword="false"/>) —
    /// they exist on every target framework the analyzer itself supports. A project that isn't multi-targeted
    /// (or where the property simply isn't visible) has nothing to conflict with, so it is also
    /// <see langword="false"/>.
    /// </summary>
    public static bool IsUnsafeAcrossTargetFrameworks(AnalyzerConfigOptions globalOptions, string typeName)
    {
        if (!CSharpKeywords.ConditionalBclTypeMinimumVersions.TryGetValue(typeName, out var minimumMajorVersion))
        {
            return false;
        }

        if (!globalOptions.TryGetValue(BuildProperty.TargetFrameworks, out var targetFrameworks))
        {
            return false;
        }

        return TargetFrameworkMonikers
            .Split(targetFrameworks)
            .Any(moniker => !TargetFrameworkMonikers.IsAtLeast(moniker, minimumMajorVersion));
    }
}
