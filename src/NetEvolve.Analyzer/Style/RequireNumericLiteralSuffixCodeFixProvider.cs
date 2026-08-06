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
/// Code fix for <see cref="RequireNumericLiteralSuffixAnalyzer">NE0012</see>. Rewrites the literal token so
/// its digit portion carries the canonical suffix, replacing whatever suffix (if any) was there before.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequireNumericLiteralSuffixCodeFixProvider))]
[Shared]
public sealed class RequireNumericLiteralSuffixCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0012);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        // getInnermostNodeForTie: an argument, ref/array-element, or other wrapper with no extra tokens of its
        // own shares the literal's exact span; without this flag FindNode returns that outer wrapper instead
        // of the literal, and AncestorsAndSelf() below would then never see it.
        var literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<LiteralExpressionSyntax>()
            .First();

        var semanticModel = (
            await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false)
        )!;
        var required = NumericLiteralSuffix.RequiredSuffix(
            semanticModel.GetTypeInfo(literal, context.CancellationToken).ConvertedType
        );
        if (required is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use the '{required}' suffix",
                cancellationToken => AddSuffixAsync(context.Document, literal, required, cancellationToken),
                equivalenceKey: "NE0012.AddSuffix"
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddSuffixAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string suffix,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
        var current = root.FindNode(literal.Span, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<LiteralExpressionSyntax>()
            .First();

        var (digits, _) = NumericLiteralSuffix.SplitSuffix(current.Token.Text);
        var newToken = SyntaxFactory.ParseToken(digits + suffix).WithTriviaFrom(current.Token);

        return document.WithSyntaxRoot(root.ReplaceToken(current.Token, newToken));
    }
}
