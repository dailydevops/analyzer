namespace NetEvolve.Analyzer.Style;

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
/// Code fix for <see cref="UseNumericLiteralSuffixOverCastAnalyzer">NE0013</see>. Replaces the cast
/// expression with a bare literal carrying the target type's canonical suffix.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseNumericLiteralSuffixOverCastCodeFixProvider))]
[Shared]
public sealed class UseNumericLiteralSuffixOverCastCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0013);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        // getInnermostNodeForTie: an argument or other wrapper with no extra tokens of its own shares the
        // cast's exact span; without this flag FindNode returns that outer wrapper instead of the cast, and
        // AncestorsAndSelf() below would then never see it.
        var cast = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<CastExpressionSyntax>()
            .First();

        var semanticModel = (
            await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false)
        )!;
        if (
            cast.Expression is not LiteralExpressionSyntax
            || NumericLiteralSuffix.RequiredSuffix(semanticModel.GetTypeInfo(cast, context.CancellationToken).Type)
                is not { } required
        )
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use the '{required}' suffix instead of the cast",
                cancellationToken => UseSuffixAsync(context.Document, cast, required, cancellationToken),
                equivalenceKey: "NE0013.UseSuffix"
            ),
            diagnostic
        );
    }

    private static async Task<Document> UseSuffixAsync(
        Document document,
        CastExpressionSyntax cast,
        string suffix,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
        var currentCast = root.FindNode(cast.Span, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<CastExpressionSyntax>()
            .First();
        var literal = (LiteralExpressionSyntax)currentCast.Expression;

        var (digits, _) = NumericLiteralSuffix.SplitSuffix(literal.Token.Text);
        var replacement = SyntaxFactory
            .LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.ParseToken(digits + suffix))
            .WithTriviaFrom(currentCast);

        return document.WithSyntaxRoot(root.ReplaceNode(currentCast, replacement));
    }
}
