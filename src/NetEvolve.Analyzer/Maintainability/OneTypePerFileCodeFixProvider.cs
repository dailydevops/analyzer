namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Code fix for <see cref="OneTypePerFileAnalyzer">NE0001</see>. Offers to rename the file to match its single
/// type, or — when the file holds several types — to move the flagged type (with its partial parts and, when
/// overload grouping is enabled, its generic overloads) into its own correctly named file.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OneTypePerFileCodeFixProvider))]
[Shared]
public sealed class OneTypePerFileCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0001);

    /// <inheritdoc />
    // Renaming and adding documents does not compose safely across many diagnostics, so no batch fix-all.
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        if (
            root.FindNode(diagnostic.Location.SourceSpan).AncestorsAndSelf().FirstOrDefault(IsTopLevelTypeDeclaration)
            is not MemberDeclarationSyntax declaration
        )
        {
            return;
        }

        if (
            !diagnostic.Properties.TryGetValue(OneTypePerFileAnalyzer.ExpectedFileNameProperty, out var expectedName)
            || string.IsNullOrEmpty(expectedName)
        )
        {
            return;
        }

        var singleType =
            diagnostic.Properties.TryGetValue(OneTypePerFileAnalyzer.SingleTypeProperty, out var single)
            && string.Equals(single, "true", StringComparison.Ordinal);

        if (singleType)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename file to '{expectedName}.cs'",
                    cancellationToken => RenameFileAsync(context.Document, expectedName!, cancellationToken),
                    equivalenceKey: "NE0001.RenameFile"
                ),
                diagnostic
            );
        }
        else if (
            !string.Equals(
                expectedName,
                Path.GetFileNameWithoutExtension(context.Document.Name),
                StringComparison.Ordinal
            )
        )
        {
            // When the target name equals the current file (two same-named types in different namespaces),
            // the move has no distinct destination file, so it is not offered; the diagnostic stands for
            // manual resolution.
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Move type to '{expectedName}.cs'",
                    cancellationToken => MoveTypeAsync(context.Document, declaration, expectedName!, cancellationToken),
                    equivalenceKey: "NE0001.MoveType"
                ),
                diagnostic
            );
        }
    }

    private static Task<Solution> RenameFileAsync(
        Document document,
        string expectedName,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var newName = expectedName + ".cs";
        var solution = document.Project.Solution.WithDocumentName(document.Id, newName);

        var newFilePath = SiblingPath(document.FilePath, newName);
        if (newFilePath is not null)
        {
            solution = solution.WithDocumentFilePath(document.Id, newFilePath);
        }

        return Task.FromResult(solution);
    }

    private static async Task<Solution> MoveTypeAsync(
        Document document,
        MemberDeclarationSyntax declaration,
        string expectedName,
        CancellationToken cancellationToken
    )
    {
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        var groupGenericOverloads = ReadGroupGenericOverloads(document);
        var moved = MatchingDeclarations(root, declaration, groupGenericOverloads).ToList();

        // Preserve the original file's final-newline style: trim trailing blank lines left by the edit, then
        // re-add a single newline only if the source had one.
        var endsWithNewline = root.ToFullString().EndsWith("\n", StringComparison.Ordinal);
        var newText = WithTrailingNewline(BuildNewFileText(root, NamespaceName(declaration), moved), endsWithNewline);

        // Move fires only when the file holds several type groups and exactly one group is relocated, so the
        // original always keeps at least one type (the last remaining single type becomes a rename instead).
        var removed = root.RemoveNodes(moved, SyntaxRemoveOptions.KeepNoTrivia)!;
        var remainingText = WithTrailingNewline(removed.ToFullString(), endsWithNewline);

        var newName = expectedName + ".cs";
        var newDocumentId = DocumentId.CreateNewId(document.Project.Id);

        return document
            .Project.Solution.WithDocumentText(document.Id, SourceText.From(remainingText))
            .AddDocument(
                newDocumentId,
                newName,
                SourceText.From(newText),
                document.Folders,
                SiblingPath(document.FilePath, newName)
            );
    }

    private static string BuildNewFileText(
        CompilationUnitSyntax root,
        string namespaceName,
        IReadOnlyList<MemberDeclarationSyntax> moved
    )
    {
        // Assemble the new file as text. Always emit a file-scoped namespace with the FULL dotted name (so a
        // type lifted out of a nested block namespace keeps its real namespace, and no block re-indentation is
        // needed — which is what corrupted multi-line string literals). Members are rendered from their full
        // text so leading doc comments travel with them.
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

        return builder.Append(string.Join("\n\n", moved.Select(RenderMember))).ToString();
    }

    private static string WithTrailingNewline(string text, bool trailingNewline) =>
        trailingNewline ? text.TrimEnd() + "\n" : text.TrimEnd();

    // Renders a moved member at column 0, keeping its leading doc comments/comments and inner blank lines but
    // dropping the surrounding blank lines and the indentation it had in its original (possibly nested) context.
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

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var indent = lines[0].Length - lines[0].TrimStart().Length;
        return string.Join(
            "\n",
            lines.Select(line => line.Length >= indent ? line.Substring(indent) : line.TrimStart())
        );
    }

    private static IEnumerable<MemberDeclarationSyntax> MatchingDeclarations(
        CompilationUnitSyntax root,
        MemberDeclarationSyntax declaration,
        bool groupGenericOverloads
    )
    {
        var name = Identifier(declaration).ValueText;
        var arity = Arity(declaration);
        var @namespace = NamespaceName(declaration);

        return root.DescendantNodes()
            .Where(IsTopLevelTypeDeclaration)
            .Cast<MemberDeclarationSyntax>()
            .Where(member =>
                string.Equals(Identifier(member).ValueText, name, StringComparison.Ordinal)
                && string.Equals(NamespaceName(member), @namespace, StringComparison.Ordinal)
                && (groupGenericOverloads || Arity(member) == arity)
            );
    }

    private static bool ReadGroupGenericOverloads(Document document)
    {
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        return options.TryGetValue(BuildProperty.GroupGenericOverloads, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SiblingPath(string? currentPath, string newName)
    {
        if (string.IsNullOrEmpty(currentPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(currentPath);
        return string.IsNullOrEmpty(directory) ? newName : Path.Combine(directory, newName);
    }

    private static bool IsTopLevelTypeDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
        && node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;

    private static SyntaxToken Identifier(MemberDeclarationSyntax member) =>
        member switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier,
            DelegateDeclarationSyntax @delegate => @delegate.Identifier,
            _ => default,
        };

    private static int Arity(MemberDeclarationSyntax member) =>
        member switch
        {
            TypeDeclarationSyntax type => type.TypeParameterList?.Parameters.Count ?? 0,
            DelegateDeclarationSyntax @delegate => @delegate.TypeParameterList?.Parameters.Count ?? 0,
            _ => 0,
        };

    private static string NamespaceName(SyntaxNode node)
    {
        var segments = new List<string>();
        for (var current = node.Parent; current is not null; current = current.Parent)
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
