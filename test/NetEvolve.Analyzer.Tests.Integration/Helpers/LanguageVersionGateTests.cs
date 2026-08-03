namespace NetEvolve.Analyzer.Tests.Integration.Helpers;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NetEvolve.Analyzer.Helpers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Direct tests for the shared <see cref="LanguageVersionGate"/> helper: it must report the consuming
/// document's effective C# language version and whether that version can compile a required one, which is what
/// the NE0004–NE0006 code fixes gate on.
/// </summary>
public sealed class LanguageVersionGateTests
{
    [Test]
    public async Task Effective_ReturnsProjectLanguageVersion()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, LanguageVersion.CSharp9);

        await Assert.That(LanguageVersionGate.Effective(document)).IsEqualTo(LanguageVersion.CSharp9);
    }

    [Test]
    public async Task Effective_Latest_MapsToConcreteVersion()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, LanguageVersion.Latest);

        // 'Latest' must be resolved to a concrete version, which is at least C# 9.0.
        await Assert
            .That((int)LanguageVersionGate.Effective(document))
            .IsGreaterThanOrEqualTo((int)LanguageVersion.CSharp9);
    }

    [Test]
    public async Task Supports_AtOrAboveRequired_True()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, LanguageVersion.CSharp9);

        await Assert.That(LanguageVersionGate.Supports(document, LanguageVersion.CSharp7)).IsTrue();
        await Assert.That(LanguageVersionGate.Supports(document, LanguageVersion.CSharp9)).IsTrue();
    }

    [Test]
    public async Task Supports_BelowRequired_False()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, LanguageVersion.CSharp7);

        await Assert.That(LanguageVersionGate.Supports(document, LanguageVersion.CSharp9)).IsFalse();
    }

    private static Document CreateDocument(AdhocWorkspace workspace, LanguageVersion languageVersion)
    {
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo
            .Create(projectId, VersionStamp.Default, "Sample", "Sample", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(languageVersion));

        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace
            .CurrentSolution.AddProject(projectInfo)
            .AddDocument(documentId, "Sample.cs", SourceText.From("public class Sample { }"));

        return solution.GetDocument(documentId)!;
    }
}
