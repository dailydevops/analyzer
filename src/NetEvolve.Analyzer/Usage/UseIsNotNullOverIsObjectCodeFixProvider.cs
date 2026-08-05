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
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// Code fix for <see cref="UseIsNotNullOverIsObjectAnalyzer">NE0006</see>. Rewrites an <c>x is object</c> type
/// check into the <c>x is not null</c> pattern. The <c>is not null</c> pattern requires <b>C# 9.0</b>, so the
/// fix is registered only when the project's effective language version supports it; below C# 9.0 the diagnostic
/// still stands but no illegal pattern is emitted. Trivia from the original expression is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseIsNotNullOverIsObjectCodeFixProvider))]
[Shared]
public sealed class UseIsNotNullOverIsObjectCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0006);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // `is not null` requires C# 9.0; below that there is no legal pattern form, so leave the diagnostic unfixed.
        if (!LanguageVersionGate.Supports(context.Document, LanguageVersion.CSharp9))
        {
            return;
        }

        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var isExpression = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .FirstOrDefault(node => node.IsKind(SyntaxKind.IsExpression));

        if (isExpression is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use 'is not null'",
                cancellationToken => RewriteAsync(context.Document, isExpression, cancellationToken),
                equivalenceKey: "NE0006.UseIsNotNull"
            ),
            diagnostic
        );
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        BinaryExpressionSyntax isExpression,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the expression in the freshly fetched root so the replaced node belongs to that same tree.
        var current = root.FindNode(isExpression.Span)
            .AncestorsAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .First(node => node.IsKind(SyntaxKind.IsExpression));

        // The Left of an `is` expression is the value being checked; rewrite `<value> is object` to
        // `<value> is not null`.
        var replacement = SyntaxFactory
            .IsPatternExpression(
                current.Left.WithoutTrivia(),
                SyntaxFactory.UnaryPattern(
                    SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))
                )
            )
            .NormalizeWhitespace()
            .WithTriviaFrom(current)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(current, replacement));
    }
}
