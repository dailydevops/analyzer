namespace NetEvolve.Analyzer.Usage;

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
/// Code fix for <see cref="RequireCancellationTokenParameterAnalyzer">NE0010</see>. Appends
/// <c>CancellationToken cancellationToken = default</c> as the last parameter of the flagged method, adding
/// a <c>using System.Threading;</c> directive when the file does not already have one in scope.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequireCancellationTokenParameterCodeFixProvider))]
[Shared]
public sealed class RequireCancellationTokenParameterCodeFixProvider : CodeFixProvider
{
    private const string SystemThreadingNamespace = "System.Threading";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0010);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add CancellationToken parameter",
                cancellationToken =>
                    AddCancellationTokenParameterAsync(context.Document, methodDeclaration, cancellationToken),
                equivalenceKey: "NE0010.RequireCancellationTokenParameter"
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddCancellationTokenParameterAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken
    )
    {
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the method in the freshly fetched root so the replaced node belongs to that same tree.
        var currentMethod = root.FindNode(methodDeclaration.Span)
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();

        var parameter = SyntaxFactory
            .Parameter(SyntaxFactory.Identifier("cancellationToken"))
            .WithType(SyntaxFactory.IdentifierName("CancellationToken").WithTrailingTrivia(SyntaxFactory.Space))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))
            );

        var updatedMethod = currentMethod.AddParameterListParameters(parameter);

        var newRoot = root.ReplaceNode(currentMethod, updatedMethod);
        newRoot = EnsureSystemThreadingUsing(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private static CompilationUnitSyntax EnsureSystemThreadingUsing(CompilationUnitSyntax root)
    {
        var namespaceDeclaration = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();

        var hasUsing = root
            .Usings.Concat(namespaceDeclaration?.Usings ?? default)
            .Any(usingDirective =>
                usingDirective.Alias is null
                && string.Equals(
                    usingDirective.Name?.ToString(),
                    SystemThreadingNamespace,
                    System.StringComparison.Ordinal
                )
            );

        if (hasUsing)
        {
            return root;
        }

        // Match the line ending of an existing using directive, if there is one; otherwise fall back to a
        // bare line feed, matching this repository's LF convention (see .gitattributes).
        var existingUsing = root.Usings.Concat(namespaceDeclaration?.Usings ?? default).FirstOrDefault();
        var trailingTrivia =
            existingUsing is not null && existingUsing.GetTrailingTrivia() is { Count: > 0 } existingTrailing
                ? existingTrailing
                : SyntaxFactory.TriviaList(SyntaxFactory.LineFeed);

        var newUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(SystemThreadingNamespace))
            .WithTrailingTrivia(trailingTrivia);

        if (root.Usings.Count > 0)
        {
            return root.WithUsings(InsertAlphabetically(root.Usings, newUsing));
        }

        if (namespaceDeclaration is NamespaceDeclarationSyntax blockNamespace && blockNamespace.Usings.Count > 0)
        {
            var updatedNamespace = blockNamespace.WithUsings(InsertAlphabetically(blockNamespace.Usings, newUsing));
            return root.ReplaceNode(blockNamespace, updatedNamespace);
        }

        if (
            namespaceDeclaration is FileScopedNamespaceDeclarationSyntax fileScopedNamespace
            && fileScopedNamespace.Usings.Count > 0
        )
        {
            var updatedNamespace = fileScopedNamespace.WithUsings(
                InsertAlphabetically(fileScopedNamespace.Usings, newUsing)
            );
            return root.ReplaceNode(fileScopedNamespace, updatedNamespace);
        }

        return root.WithUsings(
            root.Usings.Add(
                newUsing.WithLeadingTrivia(root.Usings.Count == 0 ? default : root.Usings[0].GetLeadingTrivia())
            )
        );
    }

    private static SyntaxList<UsingDirectiveSyntax> InsertAlphabetically(
        SyntaxList<UsingDirectiveSyntax> usings,
        UsingDirectiveSyntax newUsing
    )
    {
        var newUsingName = newUsing.Name?.ToString() ?? string.Empty;

        for (var index = 0; index < usings.Count; index++)
        {
            var existingName = usings[index].Name?.ToString() ?? string.Empty;
            if (string.CompareOrdinal(newUsingName, existingName) < 0)
            {
                var displacedNode = usings[index];
                var displacedLeadingTrivia = displacedNode.GetLeadingTrivia();

                // The new directive takes over any leading blank line that separated the displaced node from
                // whatever preceded it (e.g. a namespace declaration); the displaced node, no longer first,
                // keeps only its indentation, not that blank line.
                var updatedUsings = usings.Replace(
                    displacedNode,
                    displacedNode.WithLeadingTrivia(RemoveLeadingBlankLines(displacedLeadingTrivia))
                );
                return updatedUsings.Insert(index, newUsing.WithLeadingTrivia(displacedLeadingTrivia));
            }
        }

        return usings.Add(newUsing.WithLeadingTrivia(usings.Count > 0 ? usings[0].GetLeadingTrivia() : default));
    }

    private static SyntaxTriviaList RemoveLeadingBlankLines(SyntaxTriviaList trivia)
    {
        var skip = 0;
        while (skip < trivia.Count && trivia[skip].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            skip++;
        }

        return SyntaxFactory.TriviaList(trivia.Skip(skip));
    }
}
