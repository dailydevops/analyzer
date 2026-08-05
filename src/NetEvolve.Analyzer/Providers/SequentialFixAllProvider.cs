namespace NetEvolve.Analyzer.Providers;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// A custom fix-all that applies a code fix one diagnostic at a time, re-resolving diagnostics from the
/// accumulating solution after each step until it reaches a fixed point. Some fixes cannot compose through the
/// default batch fixer: NE0001's rename/move uses solution-level operations (<c>WithDocumentName</c>,
/// <c>AddDocument</c>) and overlapping edits to a single file, and NE0003's flatten is a whole-file rewrite. The
/// code fix to run and the diagnostics to match are taken from the <see cref="FixAllContext"/>; the analyzer
/// used to re-resolve between steps is supplied per rule through the constructor.
/// </summary>
internal sealed class SequentialFixAllProvider : FixAllProvider
{
    private readonly Func<DiagnosticAnalyzer> _analyzerFactory;

    /// <summary>
    /// Creates the provider for a single rule.
    /// </summary>
    /// <param name="analyzerFactory">
    /// Produces a fresh instance of the rule's analyzer, run after each step to re-resolve diagnostics against
    /// the accumulated solution.
    /// </param>
    public SequentialFixAllProvider(Func<DiagnosticAnalyzer> analyzerFactory) => _analyzerFactory = analyzerFactory;

    /// <inheritdoc />
    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() =>
        new[] { FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution };

    /// <inheritdoc />
    public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        if (fixAllContext is null)
        {
            throw new ArgumentNullException(nameof(fixAllContext));
        }

        if (!await HasFixableDiagnosticsAsync(fixAllContext).ConfigureAwait(false))
        {
            return null;
        }

        return CodeAction.Create(
            $"Fix all '{string.Join("', '", fixAllContext.DiagnosticIds)}' occurrences",
            cancellationToken => FixAllAsync(fixAllContext, cancellationToken),
            equivalenceKey: nameof(SequentialFixAllProvider)
        );
    }

    private static async Task<bool> HasFixableDiagnosticsAsync(FixAllContext fixAllContext)
    {
        if (fixAllContext.Scope == FixAllScope.Document && fixAllContext.Document is not null)
        {
            var diagnostics = await fixAllContext
                .GetDocumentDiagnosticsAsync(fixAllContext.Document)
                .ConfigureAwait(false);
            return !diagnostics.IsEmpty;
        }

        if (fixAllContext.Scope == FixAllScope.Project)
        {
            var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(fixAllContext.Project).ConfigureAwait(false);
            return !diagnostics.IsEmpty;
        }

        if (fixAllContext.Scope == FixAllScope.Solution)
        {
            foreach (var project in fixAllContext.Solution.Projects)
            {
                var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                if (!diagnostics.IsEmpty)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Applies one fix at a time, re-resolving diagnostics from the accumulating solution after each step.
    // Convergence (and NE0001's move->rename flip) falls out of the re-resolution; a diagnostic whose fix
    // registers no action (e.g. NE0001's collision case) is passed over without failing.
    private async Task<Solution> FixAllAsync(FixAllContext fixAllContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var solution = fixAllContext.Solution;
        var scope = fixAllContext.Scope;
        var documentId = fixAllContext.Document?.Id;
        var projectId = fixAllContext.Project?.Id;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var next = await TryApplyOneAsync(fixAllContext, solution, scope, documentId, projectId, cancellationToken)
                .ConfigureAwait(false);
            if (next is null)
            {
                return solution;
            }

            solution = next;
        }
    }

    private async Task<Solution?> TryApplyOneAsync(
        FixAllContext fixAllContext,
        Solution solution,
        FixAllScope scope,
        DocumentId? documentId,
        ProjectId? projectId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var id in TargetDocumentIds(solution, scope, documentId, projectId))
        {
            var changed = await TryFixDocumentAsync(fixAllContext, solution, id, cancellationToken)
                .ConfigureAwait(false);
            if (changed is not null)
            {
                return changed;
            }
        }

        return null;
    }

    // Re-resolves the rule's diagnostics in the document, then applies the first one (by source order) that
    // yields an action through the context's code fix provider. Returns null when the document has no fix.
    private async Task<Solution?> TryFixDocumentAsync(
        FixAllContext fixAllContext,
        Solution solution,
        DocumentId id,
        CancellationToken cancellationToken
    )
    {
        // The id always comes from TargetDocumentIds enumerating the current solution, so the document exists.
        cancellationToken.ThrowIfCancellationRequested();

        // The id always comes from TargetDocumentIds enumerating the current solution, so the document exists.
        var document = solution.GetDocument(id)!;

        var diagnostics = await ResolveDiagnosticsAsync(fixAllContext, solution, document, id, cancellationToken)
            .ConfigureAwait(false);
        var fixProvider = fixAllContext.CodeFixProvider;

        foreach (var diagnostic in diagnostics)
        {
            var changed = await TryApplyFixAsync(fixProvider, document, diagnostic, cancellationToken)
                .ConfigureAwait(false);
            if (changed is not null)
            {
                return changed;
            }
        }

        return null;
    }

    // The rule's diagnostics located in the document, ordered by source position, resolved from a fresh run of
    // the per-rule analyzer over the current project so each pass sees the accumulated edits.
    private async Task<List<Diagnostic>> ResolveDiagnosticsAsync(
        FixAllContext fixAllContext,
        Solution solution,
        Document document,
        DocumentId id,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var project = document.Project;
        var compilation = (await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;

        // S8949/CA2016: the cancellation-token WithAnalyzers overload is obsolete; cancellation is honored by
        // GetAnalyzerDiagnosticsAsync below.
#pragma warning disable S8949, CA2016
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(_analyzerFactory()),
            project.AnalyzerOptions
        );
#pragma warning restore S8949, CA2016

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);

        return diagnostics
            .Where(diagnostic => fixAllContext.DiagnosticIds.Contains(diagnostic.Id))
            .Where(diagnostic =>
                diagnostic.Location.SourceTree is not null
                && solution.GetDocument(diagnostic.Location.SourceTree)?.Id == id
            )
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToList();
    }

    // Registers the single-diagnostic fix and applies its first change. Returns null when no action is offered
    // (e.g. NE0001's collision case, where the target file equals the current file), skipping without failing.
    private static async Task<Solution?> TryApplyFixAsync(
        CodeFixProvider fixProvider,
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        await fixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        if (actions.Count == 0)
        {
            return null;
        }

        var operations = await actions[0].GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        return operations.OfType<ApplyChangesOperation>().FirstOrDefault()?.ChangedSolution;
    }

    // Re-enumerates the target document set from the CURRENT solution each pass (a move adds a new, already
    // compliant file), ordered deterministically by file path then name so the sequence is stable.
    private static List<DocumentId> TargetDocumentIds(
        Solution solution,
        FixAllScope scope,
        DocumentId? documentId,
        ProjectId? projectId
    )
    {
        IEnumerable<Document> documents;
        switch (scope)
        {
            case FixAllScope.Document:
                var single = solution.GetDocument(documentId);
                documents = single is null ? Enumerable.Empty<Document>() : new[] { single };
                break;
            case FixAllScope.Project:
                documents = solution.GetProject(projectId)?.Documents ?? Enumerable.Empty<Document>();
                break;
            case FixAllScope.Solution:
                documents = solution.Projects.SelectMany(project => project.Documents);
                break;
            default:
                documents = Enumerable.Empty<Document>();
                break;
        }

        return documents
            .OrderBy(document => document.FilePath ?? document.Name, StringComparer.Ordinal)
            .ThenBy(document => document.Name, StringComparer.Ordinal)
            .Select(document => document.Id)
            .ToList();
    }
}
