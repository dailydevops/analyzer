namespace NetEvolve.Analyzer.Abstractions;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

internal abstract class NetEvolveSuppressorBase : DiagnosticSuppressor
{
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var diagnosticsIds = SupportedSuppressions
            .Select(s => s.SuppressedDiagnosticId)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var diagnosticsToSuppress = context.ReportedDiagnostics.Where(d =>
            diagnosticsIds.Contains(d.Id) && ShouldSuppress(d, context.Compilation, context.CancellationToken)
        );

        foreach (var diagnostic in diagnosticsToSuppress)
        {
            var suppression = SupportedSuppressions.First(s =>
                string.Equals(s.SuppressedDiagnosticId, diagnostic.Id, StringComparison.Ordinal)
            );
            context.ReportSuppression(Suppression.Create(suppression, diagnostic));
        }
    }

    protected virtual bool ShouldSuppress(
        Diagnostic diagnostic,
        Compilation compilation,
        CancellationToken cancellationToken
    ) => false;
}
