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
/// End-to-end tests for NE0008 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the
/// documentation-comment-tree walk holds against a genuine compilation.
/// </summary>
public sealed class NativeTypeCrefAnalyzerTests
{
    private static bool IsNe0008(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0008, StringComparison.Ordinal);

    [Test]
    public async Task BareTypeNameInSummary_ReportsNe0008()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns the result as a <c>string</c>.</summary>
                public string Value() => "";
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NativeTypeCrefAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0008)).IsEqualTo(1);
    }

    [Test]
    public async Task BareTypeNameAcrossDifferentTags_ReportsOnePerOccurrence()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="value">An <c>int</c> value.</param>
                /// <returns>A <c>bool</c> result.</returns>
                public bool Check(int value) => value > 0;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NativeTypeCrefAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0008)).IsEqualTo(2);
    }

    [Test]
    public async Task BareTypeNameInCodeElement_ReportsNe0008()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns the result as a <code>string</code>.</summary>
                public string Value() => "";
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NativeTypeCrefAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0008)).IsEqualTo(1);
    }

    [Test]
    public async Task ExpressionInsideC_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Checks <c>x is string</c>.</summary>
                public bool Check(object x) => x is string;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NativeTypeCrefAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0008)).IsFalse();
    }

    [Test]
    public async Task VoidKeyword_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Does nothing, returns <c>void</c>.</summary>
                public void Run() { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NativeTypeCrefAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0008)).IsFalse();
    }
}
