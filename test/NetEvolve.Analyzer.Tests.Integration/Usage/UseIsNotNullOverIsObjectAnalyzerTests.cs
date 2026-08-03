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
/// End-to-end tests for NE0006 through the real <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/>
/// pipeline, confirming that an <c>is object</c> null check on a reference type is flagged while an
/// <c>is object</c> on a non-nullable value type (where the check is always true) is left alone.
/// </summary>
public sealed class UseIsNotNullOverIsObjectAnalyzerTests
{
    private static bool IsNe0006(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0006, StringComparison.Ordinal);

    [Test]
    public async Task ReferenceType_IsObject_ReportsNe0006()
    {
        const string source = """
            public sealed class Sample
            {
                public bool Check(string value) => value is object;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseIsNotNullOverIsObjectAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0006)).IsEqualTo(1);
    }

    [Test]
    public async Task NonNullableValueType_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                public bool Check(int value) => value is object;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseIsNotNullOverIsObjectAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0006)).IsFalse();
    }
}
