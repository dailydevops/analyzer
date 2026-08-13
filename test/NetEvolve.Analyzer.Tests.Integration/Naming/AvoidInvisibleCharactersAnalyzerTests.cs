namespace NetEvolve.Analyzer.Tests.Integration.Naming;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Naming;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for NE0014 through the real <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/>
/// pipeline, confirming a declaration whose name contains a Unicode "Format" category character is reported
/// once, at the declaration, while an otherwise identical clean declaration is not.
/// </summary>
public sealed class AvoidInvisibleCharactersAnalyzerTests
{
    // A zero-width space (U+200B) — the same category of character removed in the motivating fix.
    private const string ZeroWidthSpace = "\u200B";

    // A byte-order mark / zero-width no-break space (U+FEFF).
    private const string Bom = "\uFEFF";

    private static bool IsNe0014(Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0014, StringComparison.Ordinal);

    [Test]
    public async Task ClassNameContainsZeroWidthSpace_ReportsWarningOnce()
    {
        var source = $$"""
            public sealed class My{{ZeroWidthSpace}}Class
            {
            }

            public sealed class Consumer
            {
                public My{{ZeroWidthSpace}}Class? Field;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidInvisibleCharactersAnalyzer())
            .ConfigureAwait(false);

        var ne0014 = diagnostics.Where(IsNe0014).ToArray();
        await Assert.That(ne0014.Length).IsEqualTo(1);
        await Assert.That(ne0014[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task NamespaceSegmentContainsInvisibleCharacter_ReportsWarning()
    {
        var source = $$"""
            namespace My{{ZeroWidthSpace}}Project.Sub;

            public sealed class Sample
            {
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidInvisibleCharactersAnalyzer())
            .ConfigureAwait(false);

        var ne0014 = diagnostics.Where(IsNe0014).ToArray();
        await Assert.That(ne0014.Length).IsEqualTo(1);
        await Assert.That(ne0014[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task StrayByteOrderMarkBeforeNamespaceKeyword_ReportsWarning()
    {
        var source = $"{Bom}namespace My.Project;\n\npublic sealed class Sample\n{{\n}}\n";

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidInvisibleCharactersAnalyzer())
            .ConfigureAwait(false);

        var ne0014 = diagnostics.Where(IsNe0014).ToArray();
        await Assert.That(ne0014.Length).IsEqualTo(1);
        await Assert.That(ne0014[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task CleanIdentifiers_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                public void DoWork(int value)
                {
                    var local = value;
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidInvisibleCharactersAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0014)).IsFalse();
    }
}
