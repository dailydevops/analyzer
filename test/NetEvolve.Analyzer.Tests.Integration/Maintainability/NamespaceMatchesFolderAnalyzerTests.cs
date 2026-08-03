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
/// End-to-end tests for NE0002 through the real <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/>
/// pipeline, where the tree path and the <c>RootNamespace</c>/<c>ProjectDir</c> build properties are fully
/// deterministic — the folder-to-namespace mapping the unit verifier cannot reliably reproduce.
/// </summary>
public sealed class NamespaceMatchesFolderAnalyzerTests
{
    private const string ExpectedNamespaceKey = NamespaceMatchesFolderAnalyzer.ExpectedNamespaceProperty;

    private static readonly (string Key, string Value)[] Anchor =
    [
        ("RootNamespace", "Geometry"),
        ("ProjectDir", "/proj"),
    ];

    private static bool IsNe0002(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0002, StringComparison.Ordinal);

    [Test]
    public async Task Mismatch_ReportsNe0002()
    {
        const string source = """
            namespace Geometry.Shapes;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/proj/Shapes/Primitives/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0002)).IsEqualTo(1);
        await Assert
            .That(diagnostics.Single(IsNe0002).Properties[ExpectedNamespaceKey])
            .IsEqualTo("Geometry.Shapes.Primitives");
    }

    [Test]
    public async Task ExactMatch_ReportsNothing()
    {
        const string source = """
            namespace Geometry.Shapes.Primitives;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/proj/Shapes/Primitives/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }

    [Test]
    public async Task FileInProjectRoot_MapsToRootNamespace_MismatchReported()
    {
        const string source = """
            namespace Wrong;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/proj/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0002)).IsEqualTo(1);
        await Assert.That(diagnostics.Single(IsNe0002).Properties[ExpectedNamespaceKey]).IsEqualTo("Geometry");
    }

    [Test]
    public async Task FileInProjectRoot_MatchesRootNamespace_ReportsNothing()
    {
        const string source = """
            namespace Geometry;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/proj/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }

    [Test]
    public async Task MissingBuildProperties_ReportsNothing()
    {
        const string source = """
            namespace Wrong;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NamespaceMatchesFolderAnalyzer(), path: "/proj/Shapes/Circle.cs")
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }

    [Test]
    public async Task FileOutsideProjectDir_ReportsNothing()
    {
        const string source = """
            namespace Wrong;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/other/Shapes/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }

    [Test]
    public async Task NonIdentifierFolderSegment_ReportsNothing()
    {
        const string source = """
            namespace Wrong;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/proj/1Shapes/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }

    [Test]
    public async Task NestedNamespace_TopLevelChecked_InnerIgnored()
    {
        // Only the top-level namespace is NE0002's concern; the nested declaration is NE0003's, so the
        // top-level match here yields no NE0002 regardless of the inner name.
        const string source = """
            namespace Geometry.Shapes
            {
                namespace Inner
                {
                    public sealed class Circle { }
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(
                source,
                new NamespaceMatchesFolderAnalyzer(),
                path: "/proj/Shapes/Circle.cs",
                properties: Anchor
            )
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }

    [Test]
    public async Task WithoutFilePath_ReportsNothing()
    {
        const string source = """
            namespace Wrong;

            public sealed class Circle { }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new NamespaceMatchesFolderAnalyzer(), properties: Anchor)
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0002)).IsFalse();
    }
}
