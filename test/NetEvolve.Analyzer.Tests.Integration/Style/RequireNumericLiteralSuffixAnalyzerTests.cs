namespace NetEvolve.Analyzer.Tests.Integration.Style;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Style;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for NE0012 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline.
/// </summary>
public sealed class RequireNumericLiteralSuffixAnalyzerTests
{
    private static bool IsNe0012(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0012, StringComparison.Ordinal);

    [Test]
    public async Task LongAssignment_MissingSuffix_ReportsInfo()
    {
        const string source = """
            public sealed class Sample
            {
                public long Value = 0;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireNumericLiteralSuffixAnalyzer())
            .ConfigureAwait(false);

        var ne0012 = diagnostics.Where(IsNe0012).ToArray();
        await Assert.That(ne0012.Length).IsEqualTo(1);
        await Assert.That(ne0012[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task LongAssignment_CorrectSuffix_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                public long Value = 0L;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireNumericLiteralSuffixAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0012)).IsFalse();
    }
}
