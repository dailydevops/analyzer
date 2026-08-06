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
        var link = DiagnosticIds.HelpLink("NE0001", DiagnosticCategories.Maintainability);

        await Assert
            .That(link)
            .IsEqualTo("https://github.com/dailydevops/analyzer/blob/main/docs/rules/maintainability/ne0001.md");
    }
}
