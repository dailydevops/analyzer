namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Maintainability;

/// <summary>
/// Drives <see cref="SingleNamespacePerFileCodeFixProvider"/> end-to-end through a real <see cref="AdhocWorkspace"/>
/// from the unit suite as well, so the NE0003 flatten fix is exercised by both the unit and integration flags
/// (a line covered by only one flag counts as a partial against the patch coverage gate).
/// </summary>
internal static class SingleNamespaceCodeFixRunner
{
    private static readonly ImmutableArray<MetadataReference> _references = FrameworkReferences.All;

    public static async Task<IReadOnlyDictionary<string, string>> ApplyAsync(
        string name,
        string content,
        string filePath,
        (string Key, string Value)[]? properties = null,
        CancellationToken cancellationToken = default
    )
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = BuildSolution(workspace, projectId, name, content, filePath, properties);

        var changed = await ApplyFixAsync(solution, projectId, cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in changed.GetProject(projectId)!.Documents)
        {
            result[document.Name] = (await document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        }

        return result;
    }

    private static Solution BuildSolution(
        AdhocWorkspace workspace,
        ProjectId projectId,
        string name,
        string content,
        string filePath,
        (string Key, string Value)[]? properties
    )
    {
        var projectInfo = ProjectInfo
            .Create(projectId, VersionStamp.Default, "Sample", "Sample", LanguageNames.CSharp)
            .WithMetadataReferences(_references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var solution = workspace
            .CurrentSolution.AddProject(projectInfo)
            .AddDocument(DocumentId.CreateNewId(projectId), name, SourceText.From(content), filePath: filePath);

        if (properties is not { Length: > 0 })
        {
            return solution;
        }

        var builder = new StringBuilder("is_global = true\n");
        foreach (var (key, value) in properties)
        {
            _ = builder.Append("build_property.").Append(key).Append(" = ").Append(value).Append('\n');
        }

        return solution.AddAnalyzerConfigDocument(
            DocumentId.CreateNewId(projectId),
            ".globalconfig",
            SourceText.From(builder.ToString()),
            filePath: "/.globalconfig"
        );
    }

    private static async Task<Solution> ApplyFixAsync(
        Solution solution,
        ProjectId projectId,
        CancellationToken cancellationToken
    )
    {
        var project = solution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;

        // S8949/CA2016: the cancellation-token WithAnalyzers overload is obsolete; cancellation is honored by
        // GetAnalyzerDiagnosticsAsync below.
#pragma warning disable S8949, CA2016
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new SingleNamespacePerFileAnalyzer()),
            project.AnalyzerOptions
        );
#pragma warning restore S8949, CA2016

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        var diagnostic = diagnostics.First(d => string.Equals(d.Id, DiagnosticIds.NE0003, StringComparison.Ordinal));
        var document = solution.GetDocument(diagnostic.Location.SourceTree)!;

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        await new SingleNamespacePerFileCodeFixProvider().RegisterCodeFixesAsync(context).ConfigureAwait(false);

        if (actions.Count == 0)
        {
            return solution;
        }

        var operations = await actions[0].GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        return operations.OfType<ApplyChangesOperation>().First().ChangedSolution;
    }
}
