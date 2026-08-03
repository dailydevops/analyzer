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
/// End-to-end tests for NE0005 through the real <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/>
/// pipeline, confirming that an inequality comparison against <c>null</c> is flagged while an equivalent form
/// with no legal pattern rewrite (inside a LINQ expression tree) is left alone.
/// </summary>
public sealed class UseIsNotNullAnalyzerTests
{
    private static bool IsNe0005(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0005, StringComparison.Ordinal);

    [Test]
    public async Task ValueNotEqualsNull_ReportsNe0005()
    {
        const string source = """
            public sealed class Sample
            {
                public bool Check(string value) => value != null;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseIsNotNullAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0005)).IsEqualTo(1);
    }

    [Test]
    public async Task ExpressionTree_ReportsNothing()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;

            public sealed class Sample
            {
                public Expression<Func<string, bool>> Expr => value => value != null;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new UseIsNotNullAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0005)).IsFalse();
    }
}
