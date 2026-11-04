namespace NetEvolve.Analyzer.Tests.Unit.Verifiers;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Runs <typeparamref name="TAnalyzer"/> over a source file, hands the first diagnostic with the requested
/// identifier to <typeparamref name="TCodeFix"/> and returns the registered code actions, so tests can assert
/// what the IDE lightbulb shows (<see cref="CodeAction.Title"/>) and how Fix All groups the action
/// (<see cref="CodeAction.EquivalenceKey"/>). The regular verifiers only compare the fixed source.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer producing the diagnostic.</typeparam>
/// <typeparam name="TCodeFix">The code fix under test.</typeparam>
internal static class CodeActionProbe<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>
    /// Describes every action registered for the first <paramref name="diagnosticId"/> diagnostic as
    /// <c>Title [EquivalenceKey]</c> (actions joined with <c>" | "</c>, key <c>-</c> when absent).
    /// </summary>
    public static async Task<string> DescribeAsync(
        string source,
        string diagnosticId,
        string filePath = "Test.cs",
        LanguageVersion languageVersion = LanguageVersion.Latest,
        string? globalConfig = null
    )
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectInfo = ProjectInfo
            .Create(projectId, VersionStamp.Default, "Sample", "Sample", LanguageNames.CSharp)
            .WithMetadataReferences(FrameworkReferences.All)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(languageVersion, DocumentationMode.Diagnose));

        var solution = workspace
            .CurrentSolution.AddProject(projectInfo)
            .AddDocument(documentId, filePath, SourceText.From(source), filePath: filePath);
        if (globalConfig is not null)
        {
            solution = solution.AddAnalyzerConfigDocument(
                DocumentId.CreateNewId(projectId),
                ".globalconfig",
                SourceText.From(globalConfig),
                filePath: "/.globalconfig"
            );
        }

        var project = solution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync().ConfigureAwait(false))!;

        // S8949/CA2016: the cancellation-token WithAnalyzers overload is obsolete.
#pragma warning disable S8949, CA2016
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()), project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
#pragma warning restore S8949, CA2016

        var diagnostic = diagnostics.First(d => string.Equals(d.Id, diagnosticId, StringComparison.Ordinal));
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            solution.GetDocument(documentId)!,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None
        );
        await new TCodeFix().RegisterCodeFixesAsync(context).ConfigureAwait(false);

        return string.Join(" | ", actions.Select(a => $"{a.Title} [{a.EquivalenceKey ?? "-"}]"));
    }
}
