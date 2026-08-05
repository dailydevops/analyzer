namespace NetEvolve.Analyzer.Documentation;

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Abstractions;

/// <summary>
/// NES0001 — suppresses Meziantou.Analyzer's <c>MA0154</c> ("Use langword in XML comment") wherever
/// <see cref="UseLangwordAnalyzer">NE0007</see> already reports the same <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c>
/// element, so a consuming project that runs both analyzers doesn't get the same violation reported twice.
/// MA0154 is only suppressed when NE0007 would also flag the element — for example, MA0154 still fires on
/// its own for a native type name such as <c>&lt;c&gt;string&lt;/c&gt;</c>, which NE0007 intentionally
/// excludes (see <see cref="Helpers.CSharpKeywords"/>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseLangwordSuppressor : NetEvolveSuppressorBase
{
    internal static readonly SuppressionDescriptor Suppression = new(
        id: DiagnosticIds.NES0001,
        suppressedDiagnosticId: "MA0154",
        justification: "NE0007 already reports this <c>/<code> keyword usage; suppressed to avoid a duplicate "
            + "diagnostic from Meziantou.Analyzer."
    );

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(Suppression);

    /// <inheritdoc />
    protected override bool ShouldSuppress(Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tree = diagnostic.Location.SourceTree;
        if (tree is null)
        {
            return false;
        }

        var root = tree.GetRoot(cancellationToken);
        var element = root.FindNode(
                diagnostic.Location.SourceSpan,
                findInsideTrivia: true,
                getInnermostNodeForTie: true
            )
            .AncestorsAndSelf()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault();

        return element is not null
            && UseLangwordAnalyzer.IsCOrCodeElement(element)
            && UseLangwordAnalyzer.GetSoleKeywordContent(element) is not null;
    }
}
