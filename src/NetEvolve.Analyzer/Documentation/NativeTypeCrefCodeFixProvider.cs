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
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// Code fix for <see cref="NativeTypeCrefAnalyzer">NE0008</see>. Replaces a <c>&lt;c&gt;type&lt;/c&gt;</c> or
/// <c>&lt;code&gt;type&lt;/code&gt;</c> element with <c>&lt;see cref="type"/&gt;</c>. The rewrite is applied as
/// a plain text-span edit rather than a syntax-node replace, because replacing a node nested in structured
/// (doc comment) trivia makes Roslyn re-serialize the enclosing trivia and can normalize an unrelated line
/// ending elsewhere in the file to the platform default.
///
/// For <c>DateOnly</c>/<c>TimeOnly</c> specifically (see <see cref="ConditionalBclTypeAvailability"/>), the fix
/// is withheld in a multi-targeted project when at least one of its target frameworks doesn't have the type —
/// the analyzer only flags the doc comment because the <em>current</em> compilation resolves it, but a
/// <c>cref</c> written here would fail to resolve when that same file is rebuilt for the sibling framework.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NativeTypeCrefCodeFixProvider))]
[Shared]
public sealed class NativeTypeCrefCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0008);

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

        var typeName = string.Concat(text.TextTokens.Select(token => token.ValueText)).Trim();

        var globalOptions = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (ConditionalBclTypeAvailability.IsUnsafeAcrossTargetFrameworks(globalOptions, typeName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"""Use <see cref="{typeName}"/> instead""",
                cancellationToken => UseCrefAsync(context.Document, element.Span, typeName, cancellationToken),
                equivalenceKey: "NE0008.UseCref"
            ),
            diagnostic
        );
    }

    private static async Task<Document> UseCrefAsync(
        Document document,
        TextSpan span,
        string typeName,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newText = text.Replace(span, $"""<see cref="{typeName}"/>""");

        return document.WithText(newText);
    }
}
