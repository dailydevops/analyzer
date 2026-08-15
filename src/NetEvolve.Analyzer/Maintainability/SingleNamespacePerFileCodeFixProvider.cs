namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NetEvolve.Analyzer.Builders;
using NetEvolve.Analyzer.Helpers;
using NetEvolve.Analyzer.Providers;

/// <summary>
/// Code fix for <see cref="SingleNamespacePerFileAnalyzer">NE0003</see>. Offered only for the nested shape
/// (<c>Nested == "true"</c>): flattens the whole file to a single file-scoped namespace holding every top-level
/// type. The sibling shape is intentionally left to NE0001's move-type fix, so no action is offered there.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SingleNamespacePerFileCodeFixProvider))]
[Shared]
public sealed class SingleNamespacePerFileCodeFixProvider : CodeFixProvider
{
    private static readonly Lazy<SequentialFixAllProvider> FixAll = new(
        () => new SequentialFixAllProvider(() => new SingleNamespacePerFileAnalyzer()),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0003);

    /// <inheritdoc />
    // Flattening a nested file is a whole-file rewrite; the sequential fix-all re-resolves after each file so
    // a batch across several files converges to a fixed point.
    public override FixAllProvider? GetFixAllProvider() => FixAll.Value;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        // Only the nested shape is flattened here; the sibling shape is resolved via NE0001's move-type fix.
        var nested = string.Equals(
            diagnostic.Properties[SingleNamespacePerFileAnalyzer.NestedProperty],
            "true",
            StringComparison.Ordinal
        );
        if (!nested)
        {
            return;
        }

        var root = (CompilationUnitSyntax)
            (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var declaration = (BaseNamespaceDeclarationSyntax)
            root.FindNode(diagnostic.Location.SourceSpan)
                .AncestorsAndSelf()
                .First(node => node is BaseNamespaceDeclarationSyntax);

        var target = ResolveTargetNamespace(context.Document, declaration);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Flatten to a single namespace",
                cancellationToken => FlattenAsync(context.Document, target, cancellationToken),
                equivalenceKey: "NE0003.Flatten"
            ),
            diagnostic
        );
    }

    private static async Task<Document> FlattenAsync(
        Document document,
        string target,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        var members = NamespaceFileBuilder.TopLevelTypeDeclarations(root).ToList();
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree);
        var endsWithNewline = root.ToFullString().EndsWith("\n", StringComparison.Ordinal);
        var newText = NamespaceFileBuilder.WithTrailingNewline(
            NamespaceFileBuilder.Build(root, target, members, options),
            endsWithNewline
        );

        return document.WithText(SourceText.From(newText));
    }

    private static string ResolveTargetNamespace(Document document, BaseNamespaceDeclarationSyntax declaration)
    {
        // Prefer the folder-derived namespace so the flattened file lands where the folder layout implies; fall
        // back to the literal dotted concatenation of the nested namespace chain when no mapping is available.
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        var filePath = document.FilePath ?? string.Empty;

        return FolderNamespace.TryResolve(options, filePath, out var expected) ? expected : NamespaceChain(declaration);
    }

    private static string NamespaceChain(SyntaxNode node)
    {
        var segments = new List<string>();
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                segments.Add(namespaceDeclaration.Name.ToString());
            }
        }

        segments.Reverse();
        return string.Join(".", segments);
    }
}
