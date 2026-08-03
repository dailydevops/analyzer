namespace NetEvolve.Analyzer.Tests.Integration.Maintainability;

using System;
using System.Linq;
using System.Threading.Tasks;
using NetEvolve.Analyzer;
using NetEvolve.Analyzer.Maintainability;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for NE0003 through the real <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/>
/// pipeline. The rule is count-based and does not depend on the file path, so these hold with or without one.
/// </summary>
public sealed class SingleNamespacePerFileAnalyzerTests
{
    private static bool IsNe0003(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0003, StringComparison.Ordinal);

    [Test]
    public async Task SiblingNamespaces_ReportsNe0003()
    {
        const string source = """
            namespace First
            {
                public sealed class One { }
            }

            namespace Second
            {
                public sealed class Two { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new SingleNamespacePerFileAnalyzer(), path: "Types.cs")
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0003)).IsEqualTo(1);
    }

    [Test]
    public async Task NestedNamespaces_ReportsNe0003()
    {
        const string source = """
            namespace Outer
            {
                namespace Inner
                {
                    public sealed class Circle { }
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new SingleNamespacePerFileAnalyzer(), path: "Circle.cs")
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0003)).IsEqualTo(1);
    }

    [Test]
    public async Task SingleNamespace_ReportsNothing()
    {
        const string source = """
            namespace Geometry;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new SingleNamespacePerFileAnalyzer(), path: "Circle.cs")
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0003)).IsFalse();
    }

    [Test]
    public async Task WithoutFilePath_StillReportsNe0003()
    {
        const string source = """
            namespace First { }

            namespace Second { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new SingleNamespacePerFileAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0003)).IsEqualTo(1);
    }
}
