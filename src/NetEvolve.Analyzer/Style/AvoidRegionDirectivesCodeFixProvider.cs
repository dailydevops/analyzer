namespace NetEvolve.Analyzer.Style;

using System.Collections.Generic;
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
/// Code fix for <see cref="AvoidRegionDirectivesAnalyzer">NE0011</see>. Removes the <c>#region</c> line and its
/// matching <c>#endregion</c> line, along with any directly-adjacent blank line left behind by their removal,
/// while keeping all content between them intact. The rewrite is applied as a plain text-span edit rather than
/// a syntax-node replace, since directives are trivia, not nodes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidRegionDirectivesCodeFixProvider))]
[Shared]
public sealed class AvoidRegionDirectivesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0011);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];

        var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (trivia.GetStructure() is not RegionDirectiveTriviaSyntax regionDirective)
        {
            return;
        }

        var endRegionDirective = regionDirective
            .GetRelatedDirectives()
            .OfType<EndRegionDirectiveTriviaSyntax>()
            .FirstOrDefault();
        if (endRegionDirective is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove '#region' directive",
                cancellationToken =>
                    RemoveRegionAsync(context.Document, regionDirective, endRegionDirective, cancellationToken),
                equivalenceKey: "NE0011.RemoveRegion"
            ),
            diagnostic
        );
    }

    private static async Task<Document> RemoveRegionAsync(
        Document document,
        RegionDirectiveTriviaSyntax regionDirective,
        EndRegionDirectiveTriviaSyntax endRegionDirective,
        CancellationToken cancellationToken
    )
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var lines = text.Lines;

        var regionLine = lines.GetLineFromPosition(regionDirective.SpanStart).LineNumber;
        var endRegionLine = lines.GetLineFromPosition(endRegionDirective.SpanStart).LineNumber;

        var linesToRemove = new SortedSet<int> { regionLine, endRegionLine };

        var lineAfterRegion = regionLine + 1;
        if (lineAfterRegion < endRegionLine && IsBlankLine(lines[lineAfterRegion]))
        {
            _ = linesToRemove.Add(lineAfterRegion);
        }

        var lineBeforeEndRegion = endRegionLine - 1;
        if (lineBeforeEndRegion > regionLine && IsBlankLine(lines[lineBeforeEndRegion]))
        {
            _ = linesToRemove.Add(lineBeforeEndRegion);
        }

        var changes = linesToRemove.Select(lineNumber => new TextChange(
            lines[lineNumber].SpanIncludingLineBreak,
            string.Empty
        ));

        return document.WithText(text.WithChanges(changes));
    }

    private static bool IsBlankLine(TextLine line) => line.ToString().Trim().Length == 0;
}
