namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Code fix for <see cref="SingleNamespacePerFileAnalyzer">NE0003</see>. Offered only for the nested shape
/// (<c>Nested == "true"</c>): flattens the whole file to a single file-scoped namespace holding every top-level
/// type. The sibling shape is intentionally left to NE0001's move-type fix, so no action is offered there.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SingleNamespacePerFileCodeFixProvider))]
[Shared]
public sealed class SingleNamespacePerFileCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0003);

    /// <inheritdoc />
    // A whole-file rewrite does not compose safely across many diagnostics, so no batch fix-all.
    public override FixAllProvider? GetFixAllProvider() => null;

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
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Preserve the original file's final-newline style: trim trailing blank lines left by the rewrite, then
        // re-add a single newline only if the source had one.
        var endsWithNewline = root.ToFullString().EndsWith("\n", StringComparison.Ordinal);
        var newText = WithTrailingNewline(BuildNewFileText(root, target), endsWithNewline);

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

    private static string BuildNewFileText(CompilationUnitSyntax root, string namespaceName)
    {
        // Assemble the new file as text: the file-level usings, a single file-scoped namespace, then every
        // top-level type rendered at column 0 so its (possibly nested) indentation is dropped and leading doc
        // comments travel with it.
        var builder = new StringBuilder();

        foreach (var directive in root.Usings)
        {
            _ = builder.Append(directive.ToString()).Append('\n');
        }

        if (root.Usings.Count != 0)
        {
            _ = builder.Append('\n');
        }

        _ = builder.Append("namespace ").Append(namespaceName).Append(";\n\n");

        var members = root.DescendantNodes().Where(IsTopLevelTypeDeclaration).Cast<MemberDeclarationSyntax>();
        return builder.Append(string.Join("\n\n", members.Select(RenderMember))).ToString();
    }

    private static string WithTrailingNewline(string text, bool trailingNewline) =>
        trailingNewline ? text.TrimEnd() + "\n" : text.TrimEnd();

    // Renders a top-level member at column 0, keeping its leading doc comments/comments and inner blank lines but
    // dropping the surrounding blank lines and the indentation it had in its original (nested) context.
    private static string RenderMember(MemberDeclarationSyntax member)
    {
        var lines = member.ToFullString().Replace("\r\n", "\n").Split('\n').ToList();

        while (lines.Count != 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count != 0 && lines[lines.Count - 1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var indent = lines[0].Length - lines[0].TrimStart().Length;
        return string.Join(
            "\n",
            lines.Select(line => line.Length >= indent ? line.Substring(indent) : line.TrimStart())
        );
    }

    private static bool IsTopLevelTypeDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
        && node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;

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
