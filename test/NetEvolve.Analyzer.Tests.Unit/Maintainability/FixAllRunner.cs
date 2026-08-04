namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

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
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Maintainability;
using NetEvolve.Analyzer.Providers;

/// <summary>
/// Drives <see cref="SequentialFixAllProvider"/> end-to-end through a real <see cref="AdhocWorkspace"/> from
/// the unit suite as well, so the fix-all pipeline is exercised by both the unit and integration flags (a line
/// covered by only one flag counts as a partial against the patch coverage gate). It builds a project from named
/// documents, constructs a <see cref="FixAllContext"/> for the requested <see cref="FixAllScope"/> backed by a
/// diagnostic provider that runs <see cref="OneTypePerFileAnalyzer"/>, obtains the fix-all
/// <see cref="CodeAction"/>, applies its <see cref="ApplyChangesOperation"/>, and returns the final documents.
/// </summary>
internal static class FixAllRunner
{
    private static readonly ImmutableArray<MetadataReference> _references = FrameworkReferences.All;

    public static async Task<IReadOnlyDictionary<string, string>> FixAllAsync(
        (string Name, string Content)[] sources,
        FixAllScope scope,
        CancellationToken cancellationToken = default
    )
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = BuildSolution(workspace, projectId, sources);

        var changed = await ApplyFixAllAsync(solution, projectId, scope, cancellationToken).ConfigureAwait(false);

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
        (string Name, string Content)[] sources
    )
    {
        var projectInfo = ProjectInfo
            .Create(projectId, VersionStamp.Default, "Sample", "Sample", LanguageNames.CSharp)
            .WithMetadataReferences(_references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var (name, content) in sources)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                name,
                SourceText.From(content),
                filePath: name
            );
        }

        return solution;
    }

    private static async Task<Solution> ApplyFixAllAsync(
        Solution solution,
        ProjectId projectId,
        FixAllScope scope,
        CancellationToken cancellationToken
    )
    {
        var project = solution.GetProject(projectId)!;
        var context = await CreateContextAsync(project, scope, cancellationToken).ConfigureAwait(false);

        var fixAllProvider = new OneTypePerFileCodeFixProvider().GetFixAllProvider()!;
        var action = await fixAllProvider.GetFixAsync(context).ConfigureAwait(false);
        if (action is null)
        {
            return solution;
        }

        var operations = await action.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        return operations.OfType<ApplyChangesOperation>().First().ChangedSolution;
    }

    private static async Task<FixAllContext> CreateContextAsync(
        Project project,
        FixAllScope scope,
        CancellationToken cancellationToken
    )
    {
        var fixProvider = new OneTypePerFileCodeFixProvider();
        var diagnosticProvider = new AnalyzerDiagnosticProvider();
        var diagnosticIds = new[] { DiagnosticIds.NE0001 };

        if (scope != FixAllScope.Document)
        {
            return new FixAllContext(
                project,
                fixProvider,
                scope,
                nameof(SequentialFixAllProvider),
                diagnosticIds,
                diagnosticProvider,
                cancellationToken
            );
        }

        var trigger = await FindTriggerDocumentAsync(project, cancellationToken).ConfigureAwait(false);
        return new FixAllContext(
            trigger,
            fixProvider,
            scope,
            nameof(SequentialFixAllProvider),
            diagnosticIds,
            diagnosticProvider,
            cancellationToken
        );
    }

    private static async Task<Document> FindTriggerDocumentAsync(Project project, CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync(project, cancellationToken).ConfigureAwait(false);

        foreach (var document in project.Documents.OrderBy(document => document.Name, StringComparer.Ordinal))
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            if (diagnostics.Any(diagnostic => diagnostic.Location.SourceTree == tree))
            {
                return document;
            }
        }

        return project.Documents.First();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        Project project,
        CancellationToken cancellationToken
    )
    {
        var compilation = (await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;

        // S8949/CA2016: the cancellation-token WithAnalyzers overload is obsolete; cancellation is honored by
        // GetAnalyzerDiagnosticsAsync below.
#pragma warning disable S8949, CA2016
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new OneTypePerFileAnalyzer()),
            project.AnalyzerOptions
        );
#pragma warning restore S8949, CA2016

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        return diagnostics
            .Where(diagnostic => string.Equals(diagnostic.Id, DiagnosticIds.NE0001, StringComparison.Ordinal))
            .ToImmutableArray();
    }

    /// <summary>Supplies NE0001 diagnostics to <see cref="FixAllContext"/> by running the analyzer live.</summary>
    private sealed class AnalyzerDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken
        ) => await AnalyzeAsync(project, cancellationToken).ConfigureAwait(false);

        public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken
        )
        {
            var diagnostics = await AnalyzeAsync(project, cancellationToken).ConfigureAwait(false);
            return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree is null);
        }

        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken
        )
        {
            var diagnostics = await AnalyzeAsync(document.Project, cancellationToken).ConfigureAwait(false);
            var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == tree);
        }
    }
}
