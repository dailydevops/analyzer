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
/// Code fix for <see cref="UseIsNotNullAnalyzer">NE0005</see>. Rewrites an inequality comparison against
/// <see langword="null"/> into a pattern-based null check, choosing the form the project's language version supports:
/// <c>x is not null</c> on C# 9.0 and later, otherwise <c>!(x is null)</c> on C# 7.0 and later. Below C# 7.0 no
/// fix is offered — the diagnostic still stands, but no illegal pattern is emitted. Trivia from the original
/// comparison is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseIsNotNullCodeFixProvider))]
[Shared]
public sealed class UseIsNotNullCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0005);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Below C# 7.0 there is no legal pattern form, so leave the diagnostic unfixed.
        if (!LanguageVersionGate.Supports(context.Document, LanguageVersion.CSharp7))
        {
            return;
        }

        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var binary = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .First();

        var useNotPattern = LanguageVersionGate.Supports(context.Document, LanguageVersion.CSharp9);
        var title = useNotPattern ? "Use 'is not null'" : "Use '!(... is null)'";

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => RewriteAsync(context.Document, binary, useNotPattern, cancellationToken),
                equivalenceKey: "NE0005.UseIsNotNull"
            ),
            diagnostic
        );
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        BinaryExpressionSyntax binary,
        bool useNotPattern,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the comparison in the freshly fetched root so the replaced node belongs to that same tree.
        var current = root.FindNode(binary.Span).AncestorsAndSelf().OfType<BinaryExpressionSyntax>().First();

        var operand = IsNullLiteral(current.Left) ? current.Right : current.Left;
        var replacement = BuildReplacement(operand, useNotPattern).WithTriviaFrom(current);

        return document.WithSyntaxRoot(root.ReplaceNode(current, replacement));
    }

    // Whether the expression is the null literal, seeing through surrounding parentheses and casts (e.g.
    // `null`, `(null)`, `(string)null`), so the non-null side is selected exactly as the analyzer chose it.
    private static bool IsNullLiteral(ExpressionSyntax expression) =>
        expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => IsNullLiteral(parenthesized.Expression),
            CastExpressionSyntax cast => IsNullLiteral(cast.Expression),
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NullLiteralExpression),
            _ => false,
        };

    private static ExpressionSyntax BuildReplacement(ExpressionSyntax operand, bool useNotPattern)
    {
        var nullPattern = SyntaxFactory.ConstantPattern(
            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
        );

        if (useNotPattern)
        {
            // C# 9.0+: `operand is not null` (UnaryPattern defaults to the 'not' operator).
            return SyntaxFactory
                .IsPatternExpression(operand.WithoutTrivia(), SyntaxFactory.UnaryPattern(nullPattern))
                .NormalizeWhitespace();
        }

        // C# 7.0/8.0: `!(operand is null)`.
        return SyntaxFactory
            .PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                SyntaxFactory.ParenthesizedExpression(
                    SyntaxFactory.IsPatternExpression(operand.WithoutTrivia(), nullPattern)
                )
            )
            .NormalizeWhitespace();
    }
}
