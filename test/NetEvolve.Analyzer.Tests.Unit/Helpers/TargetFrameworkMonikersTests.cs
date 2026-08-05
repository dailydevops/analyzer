namespace NetEvolve.Analyzer.Tests.Unit.Helpers;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Helpers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Unit tests for <see cref="TargetFrameworkMonikers"/> — the generic TFM-version parsing
/// <see cref="ConditionalBclTypeAvailability"/> relies on to tell whether a conditionally-available BCL type's
/// <c>cref</c> is safe across every framework a project targets.
/// </summary>
public sealed class TargetFrameworkMonikersTests
{
    [Test]
    [Arguments("net6.0")]
    [Arguments("net7.0")]
    [Arguments("net8.0")]
    [Arguments("net10.0")]
    [Arguments("net8.0-windows10.0.19041")]
    [Arguments("NET6.0")]
    public async Task IsAtLeast_ModernTfmAtOrAboveThreshold_ReturnsTrue(string moniker)
    {
        var isAtLeast = TargetFrameworkMonikers.IsAtLeast(moniker, minimumMajorVersion: 6);

        await Assert.That(isAtLeast).IsTrue();
    }

    [Test]
    [Arguments("net5.0")]
    [Arguments("net481")]
    [Arguments("net472")]
    [Arguments("netstandard2.0")]
    [Arguments("netstandard2.1")]
    [Arguments("netcoreapp3.1")]
    public async Task IsAtLeast_OlderOrBelowThresholdTfm_ReturnsFalse(string moniker)
    {
        var isAtLeast = TargetFrameworkMonikers.IsAtLeast(moniker, minimumMajorVersion: 6);

        await Assert.That(isAtLeast).IsFalse();
    }

    [Test]
    public async Task IsAtLeast_ThresholdIsPerCall_NotHardcodedToOneVersion()
    {
        // net5.0 fails a "6 or newer" check but passes a "5 or newer" one — the threshold isn't baked into
        // the parsing, it's supplied by the caller for whichever type it's checking.
        await Assert.That(TargetFrameworkMonikers.IsAtLeast("net5.0", minimumMajorVersion: 6)).IsFalse();
        await Assert.That(TargetFrameworkMonikers.IsAtLeast("net5.0", minimumMajorVersion: 5)).IsTrue();
    }

    [Test]
    public async Task Split_CommaSeparatedList_TrimsAndDropsEmptyEntries()
    {
        var monikers = TargetFrameworkMonikers.Split("net8.0, netstandard2.0 ,,net472");

        await Assert.That(monikers).IsEquivalentTo(["net8.0", "netstandard2.0", "net472"]);
    }

    [Test]
    public async Task Split_SingleMoniker_ReturnsOneEntry()
    {
        var monikers = TargetFrameworkMonikers.Split("net8.0");

        await Assert.That(monikers).IsEquivalentTo(["net8.0"]);
    }
}
