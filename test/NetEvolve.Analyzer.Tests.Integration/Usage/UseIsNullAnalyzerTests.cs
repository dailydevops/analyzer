namespace NetEvolve.Analyzer.Tests.Integration.Usage;

using System;
using System.Linq;
using System.Threading.Tasks;
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Usage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for NE0004 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the semantic
/// operand analysis (built-in vs. user-defined <c>==</c>) holds against a genuine compilation.
/// </summary>
public sealed class UseIsNullAnalyzerTests
{
    private static bool IsNe0004(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0004, StringComparison.Ordinal);

    [Test]
    public async Task EqualsNull_ReportsNe0004()
    {
        const string source = """
            public sealed class Sample
            {
                public bool Check(object value) => value == null;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseIsNullAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0004)).IsEqualTo(1);
    }

    [Test]
    public async Task UserDefinedOperator_ReportsNothing()
    {
        const string source = """
            public sealed class Widget
            {
                public static bool operator ==(Widget left, Widget right) => false;

                public static bool operator !=(Widget left, Widget right) => true;

                public bool IsNull() => this == null;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseIsNullAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0004)).IsFalse();
    }
}
