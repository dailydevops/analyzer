namespace NetEvolve.Analyzer.Documentation;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NES0001 — suppresses Meziantou.Analyzer's <c>MA0154</c> ("Use langword in XML comment") wherever
/// <see cref="UseLangwordAnalyzer">NE0007</see> already reports the same <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c>
/// element, so a consuming project that runs both analyzers doesn't get the same violation reported twice.
/// MA0154 is only suppressed when NE0007 would also flag the element — for example, MA0154 still fires on
/// its own for a native type name such as <c>&lt;c&gt;string&lt;/c&gt;</c>, which NE0007 intentionally
/// excludes (see <see cref="Helpers.CSharpKeywords"/>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseLangwordSuppressor : DiagnosticSuppressor
{
    private const string Ma0154 = "MA0154";

    /// <summary>The suppression descriptor for <see cref="Ma0154"/>.</summary>
    internal static readonly SuppressionDescriptor Suppression = new(
        id: DiagnosticIds.NES0001,
        suppressedDiagnosticId: Ma0154,
        justification: "NE0007 already reports this <c>/<code> keyword usage; suppressed to avoid a duplicate "
            + "diagnostic from Meziantou.Analyzer."
    );

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(Suppression);

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var ma0154Diagnostics = context
            .ReportedDiagnostics.Where(diagnostic => string.Equals(diagnostic.Id, Ma0154, StringComparison.Ordinal))
            .Where(diagnostic => IsAlsoReportedByNE0007(diagnostic, context.CancellationToken));

        foreach (var diagnostic in ma0154Diagnostics)
        {
            context.ReportSuppression(Microsoft.CodeAnalysis.Diagnostics.Suppression.Create(Suppression, diagnostic));
        }
    }

    private static bool IsAlsoReportedByNE0007(Diagnostic diagnostic, CancellationToken cancellationToken)
    {
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
