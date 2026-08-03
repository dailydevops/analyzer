namespace NetEvolve.Analyzer.Maintainability;

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
/// Custom fix-all for <see cref="OneTypePerFileCodeFixProvider">NE0001</see>. The single-diagnostic fix uses
/// solution-level operations (<c>WithDocumentName</c>, <c>AddDocument</c>) and overlapping edits to a single
/// multi-type file, none of which the default batch fixer can compose. Instead this provider applies the
/// rename/move fixes one at a time and re-resolves diagnostics between each step, iterating to a fixed point.
/// </summary>
internal sealed class OneTypePerFileFixAllProvider : FixAllProvider
{
    /// <summary>The shared instance returned by <see cref="OneTypePerFileCodeFixProvider.GetFixAllProvider"/>.</summary>
    public static OneTypePerFileFixAllProvider Instance { get; } = new OneTypePerFileFixAllProvider();

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
            $"Fix all '{DiagnosticIds.NE0001}' occurrences",
            cancellationToken => FixAllAsync(fixAllContext, cancellationToken),
            equivalenceKey: nameof(OneTypePerFileFixAllProvider)
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

    // Applies one rename/move at a time, re-resolving diagnostics from the accumulating solution after each
    // step. Convergence and the move->rename flip fall out of the re-resolution; the collision case (target
    // name equals the current file) registers no action and is therefore passed over without failing.
    private static async Task<Solution> FixAllAsync(FixAllContext fixAllContext, CancellationToken cancellationToken)
    {
        var solution = fixAllContext.Solution;
        var scope = fixAllContext.Scope;
        var documentId = fixAllContext.Document?.Id;
        var projectId = fixAllContext.Project?.Id;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var next = await TryApplyOneAsync(solution, scope, documentId, projectId, cancellationToken)
                .ConfigureAwait(false);
            if (next is null)
            {
                return solution;
            }

            solution = next;
        }
    }

    private static async Task<Solution?> TryApplyOneAsync(
        Solution solution,
        FixAllScope scope,
        DocumentId? documentId,
        ProjectId? projectId,
        CancellationToken cancellationToken
    )
    {
        foreach (var id in TargetDocumentIds(solution, scope, documentId, projectId))
        {
            var changed = await TryFixDocumentAsync(solution, id, cancellationToken).ConfigureAwait(false);
            if (changed is not null)
            {
                return changed;
            }
        }

        return null;
    }

    // Runs the analyzer over the document's project, then applies the first NE0001 diagnostic in the document
    // (by source order) that yields an action. Returns null when the document has no applicable fix.
    private static async Task<Solution?> TryFixDocumentAsync(
        Solution solution,
        DocumentId id,
        CancellationToken cancellationToken
    )
    {
        var document = solution.GetDocument(id);
        if (document is null)
        {
            return null;
        }

        var diagnostics = await ResolveDiagnosticsAsync(solution, document, id, cancellationToken)
            .ConfigureAwait(false);
        var fixProvider = new OneTypePerFileCodeFixProvider();

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

    // The NE0001 diagnostics located in the document, ordered by source position, resolved from a fresh run of
    // the analyzer over the current project so each pass sees the accumulated edits.
    private static async Task<List<Diagnostic>> ResolveDiagnosticsAsync(
        Solution solution,
        Document document,
        DocumentId id,
        CancellationToken cancellationToken
    )
    {
        var project = document.Project;
        var compilation = (await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false))!;

        // S8949: the cancellation-token WithAnalyzers overload is obsolete; cancellation is honored by
        // GetAnalyzerDiagnosticsAsync below.
#pragma warning disable S8949
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new OneTypePerFileAnalyzer()),
            project.AnalyzerOptions
        );
#pragma warning restore S8949

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);

        return diagnostics
            .Where(diagnostic => string.Equals(diagnostic.Id, DiagnosticIds.NE0001, StringComparison.Ordinal))
            .Where(diagnostic =>
                diagnostic.Location.SourceTree is not null
                && solution.GetDocument(diagnostic.Location.SourceTree)?.Id == id
            )
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToList();
    }

    // Registers the single-diagnostic fix and applies its first change. Returns null when no action is offered
    // (the collision case, where the target file equals the current file), skipping without failing the batch.
    private static async Task<Solution?> TryApplyFixAsync(
        OneTypePerFileCodeFixProvider fixProvider,
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken
    )
    {
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
