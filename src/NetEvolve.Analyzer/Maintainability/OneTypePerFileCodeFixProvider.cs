namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NetEvolve.Analyzer.Builders;
using NetEvolve.Analyzer.Providers;

/// <summary>
/// Code fix for <see cref="OneTypePerFileAnalyzer">NE0001</see>. Offers to rename the file to match its single
/// type, or — when the file holds several types — to move the flagged type (with its partial parts and, when
/// overload grouping is enabled, its generic overloads) into its own correctly named file.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OneTypePerFileCodeFixProvider))]
[Shared]
public sealed class OneTypePerFileCodeFixProvider : CodeFixProvider
{
    private static readonly Lazy<SequentialFixAllProvider> FixAll = new(
        () => new SequentialFixAllProvider(() => new OneTypePerFileAnalyzer()),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0001);

    /// <inheritdoc />
    // Renaming and adding documents cannot compose through the default batch fixer, so a custom provider
    // applies the rename/move fixes sequentially and re-resolves diagnostics between each step.
    public override FixAllProvider? GetFixAllProvider() => FixAll.Value;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // The fix only handles NE0001 diagnostics from OneTypePerFileAnalyzer, which always report at a
        // top-level type identifier and always carry the ExpectedFileName/SingleType properties.
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var declaration = (MemberDeclarationSyntax)
            root.FindNode(diagnostic.Location.SourceSpan)
                .AncestorsAndSelf()
                .First(NamespaceFileBuilder.IsTopLevelTypeDeclaration);

        var expectedName = diagnostic.Properties[OneTypePerFileAnalyzer.ExpectedFileNameProperty]!;
        var singleType = string.Equals(
            diagnostic.Properties[OneTypePerFileAnalyzer.SingleTypeProperty],
            "true",
            StringComparison.Ordinal
        );

        if (singleType)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename file to '{expectedName}.cs'",
                    cancellationToken => RenameFileAsync(context.Document, expectedName, cancellationToken),
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
                    cancellationToken => MoveTypeAsync(context.Document, declaration, expectedName, cancellationToken),
                    equivalenceKey: "NE0001.MoveType"
                ),
                diagnostic
            );
        }
    }

    private static async Task<Solution> RenameFileAsync(
        Document document,
        string expectedName,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Changing an existing document's FilePath/Name is rejected by some workspace hosts (e.g. Visual
        // Studio's VisualStudioWorkspaceImpl throws InvalidOperationException). Adding a new document and
        // removing the old one is supported everywhere, so use the same approach as MoveTypeAsync.
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newName = expectedName + ".cs";
        var newDocumentId = DocumentId.CreateNewId(document.Project.Id);

        return document
            .Project.Solution.RemoveDocument(document.Id)
            .AddDocument(newDocumentId, newName, text, document.Folders, SiblingPath(document.FilePath!, newName));
    }

    private static async Task<Solution> MoveTypeAsync(
        Document document,
        MemberDeclarationSyntax declaration,
        string expectedName,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        var groupGenericOverloads = ReadGroupGenericOverloads(document);
        var moved = MatchingDeclarations(root, declaration, groupGenericOverloads).ToList();

        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree);
        var endsWithNewline = root.ToFullString().EndsWith("\n", StringComparison.Ordinal);
        var newText = NamespaceFileBuilder.WithTrailingNewline(
            NamespaceFileBuilder.Build(root, NamespaceName(declaration), moved, options),
            endsWithNewline
        );

        // Move fires only when the file holds several type groups and exactly one group is relocated, so the
        // original always keeps at least one type (the last remaining single type becomes a rename instead).
        var removed = root.RemoveNodes(moved, SyntaxRemoveOptions.KeepNoTrivia)!;
        var remainingText = NamespaceFileBuilder.WithTrailingNewline(removed.ToFullString(), endsWithNewline);

        var newName = expectedName + ".cs";
        var newDocumentId = DocumentId.CreateNewId(document.Project.Id);

        return document
            .Project.Solution.WithDocumentText(document.Id, SourceText.From(remainingText))
            .AddDocument(
                newDocumentId,
                newName,
                SourceText.From(newText),
                document.Folders,
                SiblingPath(document.FilePath!, newName)
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

        return NamespaceFileBuilder
            .TopLevelTypeDeclarations(root)
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

    private static string SiblingPath(string currentPath, string newName)
    {
        var directory = Path.GetDirectoryName(currentPath);
        return string.IsNullOrEmpty(directory) ? newName : Path.Combine(directory, newName);
    }

    private static SyntaxToken Identifier(MemberDeclarationSyntax member) =>
        member is BaseTypeDeclarationSyntax type ? type.Identifier : ((DelegateDeclarationSyntax)member).Identifier;

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
