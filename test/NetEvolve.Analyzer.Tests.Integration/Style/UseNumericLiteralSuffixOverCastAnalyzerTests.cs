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
/// End-to-end tests for NE0013 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline.
/// </summary>
public sealed class UseNumericLiteralSuffixOverCastAnalyzerTests
{
    private static bool IsNe0013(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0013, StringComparison.Ordinal);

    [Test]
    public async Task CastToLong_ReportsInfo()
    {
        const string source = """
            public sealed class Sample
            {
                public long Value = (long)0;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseNumericLiteralSuffixOverCastAnalyzer())
            .ConfigureAwait(false);

        var ne0013 = diagnostics.Where(IsNe0013).ToArray();
        await Assert.That(ne0013.Length).IsEqualTo(1);
        await Assert.That(ne0013[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task CastToInt_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                public int Value = (int)0;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseNumericLiteralSuffixOverCastAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0013)).IsFalse();
    }
}
