namespace NetEvolve.Analyzer.Builders;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    /// Assembles the new file as text: the file-level usings, an optional file-scoped namespace with the FULL
    /// dotted <paramref name="namespaceName"/> (omitted when empty), then every member in
    /// <paramref name="members"/> rendered from its full text so leading doc comments travel with it.
    /// </summary>
    public static string Build(
        CompilationUnitSyntax root,
        string namespaceName,
        IReadOnlyList<MemberDeclarationSyntax> members
    )
    {
        var builder = new StringBuilder();

        foreach (var directive in root.Usings)
        {
            _ = builder.Append(directive.ToString()).Append('\n');
        }

        if (root.Usings.Count != 0)
        {
            _ = builder.Append('\n');
        }

        if (namespaceName.Length != 0)
        {
            _ = builder.Append("namespace ").Append(namespaceName).Append(";\n\n");
        }

        return builder.Append(string.Join("\n\n", members.Select(RenderMember))).ToString();
    }

    /// <summary>
    /// Preserves the original file's final-newline style: trims trailing blank lines left by an edit, then
    /// re-adds a single newline only when <paramref name="trailingNewline"/> is <see langword="true"/>.
    /// </summary>
    public static string WithTrailingNewline(string text, bool trailingNewline) =>
        trailingNewline ? text.TrimEnd() + "\n" : text.TrimEnd();

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
