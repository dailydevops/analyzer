namespace NetEvolve.Analyzer.Documentation;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Code fix for <see cref="UseLangwordAnalyzer">NE0007</see>. Replaces a <c>&lt;c&gt;keyword&lt;/c&gt;</c>
/// element with <c>&lt;see langword="keyword"/&gt;</c>. The rewrite is applied as a plain text-span edit
/// rather than a syntax-node replace, because replacing a node nested in structured (doc comment) trivia makes
/// Roslyn re-serialize the enclosing trivia and can normalize an unrelated line ending elsewhere in the file
/// to the platform default.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseLangwordCodeFixProvider))]
[Shared]
public sealed class UseLangwordCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0007);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var element = root.FindNode(
                diagnostic.Location.SourceSpan,
                findInsideTrivia: true,
                getInnermostNodeForTie: true
            )
            .AncestorsAndSelf()
            .OfType<XmlElementSyntax>()
            .First();

        if (element.Content[0] is not XmlTextSyntax text)
        {
            return;
        }

        var keyword = string.Concat(text.TextTokens.Select(token => token.ValueText)).Trim();

        context.RegisterCodeFix(
            CodeAction.Create(
                $"""Use <see langword="{keyword}"/> instead""",
                cancellationToken => UseLangwordAsync(context.Document, element.Span, keyword, cancellationToken),
                equivalenceKey: "NE0007.UseLangword"
            ),
            diagnostic
        );
    }

    private static async Task<Document> UseLangwordAsync(
        Document document,
        TextSpan span,
        string keyword,
        CancellationToken cancellationToken
    )
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newText = text.Replace(span, $"""<see langword="{keyword}"/>""");

        return document.WithText(newText);
    }
}
