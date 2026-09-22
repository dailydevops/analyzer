namespace NetEvolve.Analyzer.Maintainability;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Code fix for <see cref="AvoidRedundantCollectionClearAnalyzer">NE0015</see>. Removes the redundant
/// <c>Clear()</c> statement outright; removal is independent of the project's language version, unlike a
/// pattern-based rewrite.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidRedundantCollectionClearCodeFixProvider))]
[Shared]
public sealed class AvoidRedundantCollectionClearCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0015);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var invocation = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .First();

        if (invocation.Parent is not ExpressionStatementSyntax statement)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove redundant 'Clear()' call",
                cancellationToken => RemoveStatementAsync(context.Document, statement, cancellationToken),
                equivalenceKey: "NE0015.RemoveRedundantClear"
            ),
            diagnostic
        );
    }

    private static async Task<Document> RemoveStatementAsync(
        Document document,
        ExpressionStatementSyntax statement,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the statement in the freshly fetched root so the removed node belongs to that same tree.
        var current = root.FindNode(statement.Span).AncestorsAndSelf().OfType<ExpressionStatementSyntax>().First();

        var updatedRoot = root.RemoveNode(current, SyntaxRemoveOptions.KeepTrailingTrivia)!;
        return document.WithSyntaxRoot(updatedRoot);
    }
}
