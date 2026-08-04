namespace NetEvolve.Analyzer.Tests.Integration.Documentation;

using System;
using System.Linq;
using System.Threading.Tasks;
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Documentation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for NE0007 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the
/// documentation-comment-tree walk holds against a genuine compilation.
/// </summary>
public sealed class UseLangwordAnalyzerTests
{
    private static bool IsNe0007(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0007, StringComparison.Ordinal);

    [Test]
    public async Task BareKeywordInSummary_ReportsNe0007()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns <c>true</c> on success.</summary>
                public bool Succeeded() => true;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseLangwordAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0007)).IsEqualTo(1);
    }

    [Test]
    public async Task BareKeywordAcrossDifferentTags_ReportsOnePerOccurrence()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="strict">When <c>true</c>, validation is stricter.</param>
                /// <returns><c>true</c> on success.</returns>
                public bool Check(bool strict) => strict;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseLangwordAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0007)).IsEqualTo(2);
    }

    [Test]
    public async Task BareKeywordInCodeElement_ReportsNe0007()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns <code>true</code> on success.</summary>
                public bool Succeeded() => true;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseLangwordAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0007)).IsEqualTo(1);
    }

    [Test]
    public async Task ExpressionInsideC_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Checks <c>x == null</c>.</summary>
                public bool Check(object x) => x == null;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseLangwordAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0007)).IsFalse();
    }
}
