namespace NetEvolve.Analyzer.Tests.Unit;

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Validates the shared diagnostic infrastructure (identifiers and categories) that every rule builds on.
/// These are the seed tests for the scaffold; rule-specific tests live next to each rule's category folder.
/// </summary>
public sealed class DiagnosticInfrastructureTests
{
    [Test]
    public async Task Prefix_IsNe() => await Assert.That(DiagnosticIds.DiagnosticPrefix).IsEqualTo("NE");

    [Test]
    public async Task HelpLink_BuildsExpectedDocumentationUrl()
    {
        var link = DiagnosticIds.HelpLink("NE0001");

        await Assert.That(link).IsEqualTo("https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0001.md");
    }

    [Test]
    public async Task Categories_UseTheStandardMicrosoftNames()
    {
        await Assert.That(DiagnosticCategories.Design).IsEqualTo("Design");
        await Assert.That(DiagnosticCategories.Documentation).IsEqualTo("Documentation");
        await Assert.That(DiagnosticCategories.Globalization).IsEqualTo("Globalization");
        await Assert.That(DiagnosticCategories.Interoperability).IsEqualTo("Interoperability");
        await Assert.That(DiagnosticCategories.Maintainability).IsEqualTo("Maintainability");
        await Assert.That(DiagnosticCategories.Naming).IsEqualTo("Naming");
        await Assert.That(DiagnosticCategories.Performance).IsEqualTo("Performance");
        await Assert.That(DiagnosticCategories.Reliability).IsEqualTo("Reliability");
        await Assert.That(DiagnosticCategories.Security).IsEqualTo("Security");
        await Assert.That(DiagnosticCategories.Style).IsEqualTo("Style");
        await Assert.That(DiagnosticCategories.Usage).IsEqualTo("Usage");
    }
}
