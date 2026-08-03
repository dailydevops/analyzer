namespace NetEvolve.Analyzer.Maintainability;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Code fix for <see cref="NamespaceMatchesFolderAnalyzer">NE0002</see>. Rewrites the flagged namespace
/// declaration's name to the folder-derived namespace carried on the diagnostic, keeping the change a local
/// document text edit (the file is not moved). Independent per-namespace edits merge, so batch fix-all applies.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamespaceMatchesFolderCodeFixProvider))]
[Shared]
public sealed class NamespaceMatchesFolderCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0002);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // The fix only handles NE0002 diagnostics from NamespaceMatchesFolderAnalyzer, which always report at a
        // top-level namespace name and always carry the ExpectedNamespace property.
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var declaration = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .First();

        var expected = diagnostic.Properties[NamespaceMatchesFolderAnalyzer.ExpectedNamespaceProperty]!;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Change namespace to '{expected}'",
                cancellationToken => ChangeNamespaceAsync(context.Document, declaration, expected, cancellationToken),
                equivalenceKey: "NE0002.ChangeNamespace"
            ),
            diagnostic
        );
    }

    private static async Task<Document> ChangeNamespaceAsync(
        Document document,
        BaseNamespaceDeclarationSyntax declaration,
        string expected,
        CancellationToken cancellationToken
    )
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the declaration in the freshly fetched root so the replaced node belongs to that same tree.
        var current = root.FindNode(declaration.Span)
            .AncestorsAndSelf()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .First();

        var oldName = current.Name;
        var newName = SyntaxFactory.ParseName(expected).WithTriviaFrom(oldName);

        return document.WithSyntaxRoot(root.ReplaceNode(oldName, newName));
    }
}
