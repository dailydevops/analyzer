namespace NetEvolve.Analyzer.Helpers;

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Parses target framework monikers well enough to answer one question: is this moniker at least a given
/// unified TFM major version? Backs <see cref="ConditionalBclTypeAvailability"/>, which uses it to check each
/// <see cref="CSharpKeywords.ConditionalBclTypeMinimumVersions"/> entry against its own minimum version —
/// nothing here is specific to any one conditionally-available type.
/// </summary>
internal static class TargetFrameworkMonikers
{
    // Matches only the modern, unified TFM shape ("net5.0", "net8.0-windows10.0.19041", ...). Older monikers
    // — ".NETFramework" ("net472", "net48"), ".NETStandard" ("netstandard2.0", "netstandard2.1"), and
    // ".NETCoreApp" ("netcoreapp3.1") — never match: each has a non-digit character glued right after "net",
    // where this pattern requires a digit.
    private static readonly Regex UnifiedTfmPattern = new(
        @"^net(?<major>\d+)\.\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    /// <summary>
    /// Splits a comma-separated target framework list — <c>BuildProperty.TargetFrameworks</c>'s re-joined
    /// form of MSBuild's semicolon-separated <c>$(TargetFrameworks)</c> (see there for why) — into its
    /// individual monikers, trimming whitespace and dropping empty entries.
    /// </summary>
    public static string[] Split(string targetFrameworks) =>
        [.. targetFrameworks.Split(',').Select(moniker => moniker.Trim()).Where(moniker => moniker.Length > 0)];

    /// <summary>
    /// Whether <paramref name="moniker"/> is a unified TFM ("net5.0" and up, ignoring a platform suffix) whose
    /// major version is at least <paramref name="minimumMajorVersion"/>. Any older-style moniker (.NET
    /// Framework, .NET Standard, .NET Core App) never qualifies, regardless of the threshold — none of them
    /// share the unified TFM's version numbering.
    /// </summary>
    public static bool IsAtLeast(string moniker, int minimumMajorVersion) =>
        TryGetUnifiedMajorVersion(moniker, out var major) && major >= minimumMajorVersion;

    private static bool TryGetUnifiedMajorVersion(string moniker, out int major)
    {
        var match = UnifiedTfmPattern.Match(moniker.Trim());
        if (!match.Success)
        {
            major = 0;
            return false;
        }

        major = int.Parse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }
}
