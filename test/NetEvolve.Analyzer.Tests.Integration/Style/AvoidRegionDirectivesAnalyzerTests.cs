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
/// End-to-end tests for NE0011 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the reported
/// severity for both a member-body-nested and a type-level <c>#region</c>.
/// </summary>
public sealed class AvoidRegionDirectivesAnalyzerTests
{
    private static bool IsNe0011(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0011, StringComparison.Ordinal);

    [Test]
    public async Task InsideMethodBody_ReportsWarning()
    {
        const string source = """
            public sealed class Sample
            {
                public void DoWork()
                {
                    #region Logic
                    var x = 1;
                    #endregion
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRegionDirectivesAnalyzer())
            .ConfigureAwait(false);

        var ne0011 = diagnostics.Where(IsNe0011).ToArray();
        await Assert.That(ne0011.Length).IsEqualTo(1);
        await Assert.That(ne0011[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task WrappingWholeClassAtNamespaceLevel_ReportsInfo()
    {
        const string source = """
            namespace SampleNamespace
            {
                #region Types
                public sealed class Sample
                {
                    public void DoWork() { }
                }
                #endregion
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRegionDirectivesAnalyzer())
            .ConfigureAwait(false);

        var ne0011 = diagnostics.Where(IsNe0011).ToArray();
        await Assert.That(ne0011.Length).IsEqualTo(1);
        await Assert.That(ne0011[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task WrappingUsingDirectivesAtFileLevel_ReportsInfo()
    {
        const string source = """
            #region Usings
            using System;
            #endregion

            public sealed class Sample
            {
                public void DoWork() => Console.WriteLine();
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRegionDirectivesAnalyzer())
            .ConfigureAwait(false);

        var ne0011 = diagnostics.Where(IsNe0011).ToArray();
        await Assert.That(ne0011.Length).IsEqualTo(1);
        await Assert.That(ne0011[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task NoDirectives_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                public void DoWork() { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRegionDirectivesAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0011)).IsFalse();
    }
}
