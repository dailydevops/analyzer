namespace NetEvolve.Analyzer.Builders;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Shared file-text assembly for the file-organization code fixes. Builds a new source file from a set of
/// top-level type declarations under a single file-scoped namespace, rendering each member at column 0 so any
/// indentation from a nested (block) namespace is dropped without re-indenting — which is what corrupted
/// multi-line string literals. Used by NE0001's move-type fix and NE0003's flatten fix so both emit identical
/// layout.
/// </summary>
internal static class NamespaceFileBuilder
{
    /// <summary>
    /// Assembles the new file as text: the usings, an optional file-scoped namespace with the FULL dotted
    /// <paramref name="namespaceName"/> (omitted when empty), then every member in <paramref name="members"/>
    /// rendered from its full text so leading doc comments travel with it. Usings are placed before or after the
    /// namespace declaration per the project's <c>csharp_using_directive_placement</c> .editorconfig setting
    /// (<paramref name="options"/>), so the fix does not fight the project's own convention.
    /// </summary>
    public static string Build(
        CompilationUnitSyntax root,
        string namespaceName,
        IReadOnlyList<MemberDeclarationSyntax> members,
        AnalyzerConfigOptions? options = null
    )
    {
        var builder = new StringBuilder();
        var usings = CollectUsings(root, members);
        var insideNamespace =
            namespaceName.Length != 0
            && options is not null
            && options.TryGetValue("csharp_using_directive_placement", out var placement)
            && placement.StartsWith("inside_namespace", StringComparison.OrdinalIgnoreCase);

        if (!insideNamespace)
        {
            AppendUsings(builder, usings);
        }

        if (namespaceName.Length != 0)
        {
            _ = builder.Append("namespace ").Append(namespaceName).Append(";\n\n");
        }

        if (insideNamespace)
        {
            AppendUsings(builder, usings);
        }

        return builder.Append(string.Join("\n\n", members.Select(RenderMember))).ToString();
    }

    private static void AppendUsings(StringBuilder builder, List<UsingDirectiveSyntax> usings)
    {
        foreach (var directive in usings)
        {
            _ = builder.Append(directive.ToString()).Append('\n');
        }

        if (usings.Count != 0)
        {
            _ = builder.Append('\n');
        }
    }

    /// <summary>
    /// Preserves the original file's final-newline style: trims trailing blank lines left by an edit, then
    /// re-adds a single newline only when <paramref name="trailingNewline"/> is <see langword="true"/>.
    /// </summary>
    public static string WithTrailingNewline(string text, bool trailingNewline) =>
        trailingNewline ? text.TrimEnd() + "\n" : text.TrimEnd();

    // File-level usings live on the CompilationUnitSyntax, but usings declared inside a block-scoped
    // `namespace X { using Y; ... }` live on that NamespaceDeclarationSyntax instead — never visited via
    // root.Usings. Collect both, deduped by text, so moved/flattened members keep the usings their original
    // context relied on.
    private static List<UsingDirectiveSyntax> CollectUsings(
        CompilationUnitSyntax root,
        IReadOnlyList<MemberDeclarationSyntax> members
    )
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var usings = new List<UsingDirectiveSyntax>();

        void AddRange(SyntaxList<UsingDirectiveSyntax> directives) =>
            usings.AddRange(directives.Where(directive => seen.Add(directive.ToString())));

        AddRange(root.Usings);
        foreach (var member in members)
        {
            foreach (var namespaceDeclaration in member.Ancestors().OfType<BaseNamespaceDeclarationSyntax>())
            {
                AddRange(namespaceDeclaration.Usings);
            }
        }

        return usings;
    }

    /// <summary>The top-level type declarations (block- or file-scoped) of <paramref name="root"/>.</summary>
    public static IEnumerable<MemberDeclarationSyntax> TopLevelTypeDeclarations(CompilationUnitSyntax root) =>
        root.DescendantNodes().Where(IsTopLevelTypeDeclaration).Cast<MemberDeclarationSyntax>();

    /// <summary>
    /// Whether <paramref name="node"/> is a top-level type declaration — a type or delegate declared directly
    /// under a namespace or the compilation unit.
    /// </summary>
    public static bool IsTopLevelTypeDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
        && node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;

    // Renders a member at column 0, keeping its leading doc comments/comments and inner blank lines but dropping
    // the surrounding blank lines and the indentation it had in its original (possibly nested) context.
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
}
