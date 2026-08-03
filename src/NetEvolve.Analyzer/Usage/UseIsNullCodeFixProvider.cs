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
/// Code fix for <see cref="UseIsNullAnalyzer">NE0004</see>. Rewrites a <c>x == null</c> / <c>null == x</c>
/// comparison into the <c>x is null</c> constant pattern, preserving the original expression's trivia. The
/// pattern needs C# 7.0, so the fix registers only when the document's effective language version supports it;
/// on an older version the diagnostic still stands but no (uncompilable) fix is offered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseIsNullCodeFixProvider))]
[Shared]
public sealed class UseIsNullCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0004);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // The 'is null' constant pattern is a C# 7.0 feature; on an older LangVersion the diagnostic remains
        // but there is no legal rewrite, so register nothing.
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

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use 'is null' pattern",
                cancellationToken => UseIsNullAsync(context.Document, binary, cancellationToken),
                equivalenceKey: "NE0004.UseIsNull"
            ),
            diagnostic
        );
    }

    private static async Task<Document> UseIsNullAsync(
        Document document,
        BinaryExpressionSyntax binary,
        CancellationToken cancellationToken
    )
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the comparison in the freshly fetched root so the replaced node belongs to that same tree.
        var current = root.FindNode(binary.Span).AncestorsAndSelf().OfType<BinaryExpressionSyntax>().First();

        // The operand is whichever side is not the null literal; IsNullLiteral unwraps parentheses and casts so
        // a written form such as `(object)null == value` still selects `value`, matching what the analyzer saw.
        var operand = IsNullLiteral(current.Left) ? current.Right : current.Left;

        var replacement = SyntaxFactory
            .IsPatternExpression(
                operand.WithoutTrivia(),
                SyntaxFactory
                    .Token(SyntaxKind.IsKeyword)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))
            )
            .WithTriviaFrom(current);

        return document.WithSyntaxRoot(root.ReplaceNode(current, replacement));
    }

    // Whether the expression is the null literal, seeing through surrounding parentheses and casts (e.g.
    // `null`, `(null)`, `(object)null`). The analyzer flags only a literal null, so this fully mirrors it.
    private static bool IsNullLiteral(ExpressionSyntax expression) =>
        expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => IsNullLiteral(parenthesized.Expression),
            CastExpressionSyntax cast => IsNullLiteral(cast.Expression),
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NullLiteralExpression),
            _ => false,
        };
}
