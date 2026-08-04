namespace NetEvolve.Analyzer.Tests.Integration.Documentation;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Abstractions;
using NetEvolve.Analyzer.Documentation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for <see cref="UseLangwordSuppressor"/> (NES0001) and, by extension,
/// <see cref="NetEvolveSuppressorBase"/>, through the real <see cref="CompilationWithAnalyzers"/> pipeline.
/// A <see cref="FakeMa0154Analyzer"/> stands in for Meziantou.Analyzer (not referenceable as a library from
/// this project); its report locations are configurable so every branch of
/// <see cref="UseLangwordSuppressor.ShouldSuppress"/> can be exercised deterministically — a bare keyword
/// (suppressed), non-keyword content (not suppressed), a location with no enclosing <c>&lt;c&gt;</c>/
/// <c>&lt;code&gt;</c> element at all, a location inside a differently named XML element, and a diagnostic
/// with no source tree.
/// </summary>
public sealed class UseLangwordSuppressorTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        DiagnosticSuppressor suppressor,
        DiagnosticAnalyzer fakeMa0154Analyzer
    )
    {
        var compilation = AnalyzerCompiler.CreateCompilation(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new UseLangwordAnalyzer(),
            suppressor,
            fakeMa0154Analyzer
        );
        var options = new CompilationWithAnalyzersOptions(
            options: new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true
        );

        var withAnalyzers = compilation.WithAnalyzers(analyzers, options);

        return await withAnalyzers.GetAllDiagnosticsAsync().ConfigureAwait(false);
    }

    private static Diagnostic SingleMa0154(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Single(diagnostic => string.Equals(diagnostic.Id, "MA0154", StringComparison.Ordinal));

    [Test]
    public async Task BareKeyword_Ma0154IsSuppressed()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns <c>true</c> on success.</summary>
                public bool Succeeded() => true;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(
                source,
                new UseLangwordSuppressor(),
                new FakeMa0154Analyzer(FakeMa0154Analyzer.AllCOrCodeElements)
            )
            .ConfigureAwait(false);

        await Assert.That(SingleMa0154(diagnostics).IsSuppressed).IsTrue();
    }

    [Test]
    public async Task NonKeywordContent_Ma0154IsNotSuppressed()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Checks <c>x == null</c>.</summary>
                public bool Check(object x) => x == null;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(
                source,
                new UseLangwordSuppressor(),
                new FakeMa0154Analyzer(FakeMa0154Analyzer.AllCOrCodeElements)
            )
            .ConfigureAwait(false);

        await Assert.That(SingleMa0154(diagnostics).IsSuppressed).IsFalse();
    }

    [Test]
    public async Task LocationOutsideAnyXmlElement_Ma0154IsNotSuppressed()
    {
        const string source = """
            public sealed class Sample
            {
                public bool Succeeded() => true;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(
                source,
                new UseLangwordSuppressor(),
                new FakeMa0154Analyzer(FakeMa0154Analyzer.FirstMethodIdentifier)
            )
            .ConfigureAwait(false);

        await Assert.That(SingleMa0154(diagnostics).IsSuppressed).IsFalse();
    }

    [Test]
    public async Task LocationInsideNonCOrCodeElement_Ma0154IsNotSuppressed()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="strict">Whether the check is strict.</param>
                public bool Check(bool strict) => strict;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(
                source,
                new UseLangwordSuppressor(),
                new FakeMa0154Analyzer(FakeMa0154Analyzer.FirstParamElement)
            )
            .ConfigureAwait(false);

        await Assert.That(SingleMa0154(diagnostics).IsSuppressed).IsFalse();
    }

    [Test]
    public async Task NoSourceTree_Ma0154IsNotSuppressed()
    {
        const string source = """
            public sealed class Sample
            {
                public bool Succeeded() => true;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(
                source,
                new UseLangwordSuppressor(),
                new FakeMa0154Analyzer(reportAtCompilationLevel: true)
            )
            .ConfigureAwait(false);

        await Assert.That(SingleMa0154(diagnostics).IsSuppressed).IsFalse();
    }

    [Test]
    public async Task SuppressorWithDefaultShouldSuppress_NeverSuppresses()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns <c>true</c> on success.</summary>
                public bool Succeeded() => true;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(
                source,
                new NoOpSuppressor(),
                new FakeMa0154Analyzer(FakeMa0154Analyzer.AllCOrCodeElements)
            )
            .ConfigureAwait(false);

        await Assert.That(SingleMa0154(diagnostics).IsSuppressed).IsFalse();
    }

    /// <summary>
    /// Stands in for Meziantou.Analyzer's MA0154, with a configurable <paramref name="selectLocations"/> so
    /// tests can target every branch of <see cref="UseLangwordSuppressor.ShouldSuppress"/>, or report a
    /// single diagnostic with no source tree at all via <paramref name="reportAtCompilationLevel"/>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class FakeMa0154Analyzer(
        Func<SyntaxNode, IEnumerable<Location>>? selectLocations = null,
        bool reportAtCompilationLevel = false
    ) : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Descriptor = new(
            id: "MA0154",
            title: "Use langword in XML comment",
            messageFormat: "Use langword in XML comment",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true
        );

        /// <summary>Reports on every <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c> element, regardless of content.</summary>
        internal static IEnumerable<Location> AllCOrCodeElements(SyntaxNode root) =>
            root.DescendantNodes(descendIntoTrivia: true)
                .OfType<XmlElementSyntax>()
                .Where(UseLangwordAnalyzer.IsCOrCodeElement)
                .Select(element => element.GetLocation());

        /// <summary>Reports on the first method's identifier — a location outside any XML element.</summary>
        internal static IEnumerable<Location> FirstMethodIdentifier(SyntaxNode root) =>
            root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Take(1)
                .Select(method => method.Identifier.GetLocation());

        /// <summary>Reports on the first &lt;param&gt; element — a non-&lt;c&gt;/&lt;code&gt; XML element.</summary>
        internal static IEnumerable<Location> FirstParamElement(SyntaxNode root) =>
            root.DescendantNodes(descendIntoTrivia: true)
                .OfType<XmlElementSyntax>()
                .Where(element =>
                    string.Equals(element.StartTag.Name.LocalName.ValueText, "param", StringComparison.Ordinal)
                )
                .Take(1)
                .Select(element => element.GetLocation());

        private readonly Func<SyntaxNode, IEnumerable<Location>> _selectLocations =
            selectLocations ?? AllCOrCodeElements;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            if (reportAtCompilationLevel)
            {
                context.RegisterCompilationAction(compilationContext =>
                    compilationContext.ReportDiagnostic(Diagnostic.Create(Descriptor, Location.None))
                );
                return;
            }

            context.RegisterSyntaxTreeAction(treeContext =>
            {
                var root = treeContext.Tree.GetRoot(treeContext.CancellationToken);
                foreach (var location in _selectLocations(root))
                {
                    treeContext.ReportDiagnostic(Diagnostic.Create(Descriptor, location));
                }
            });
        }
    }

    /// <summary>A suppressor that never overrides <see cref="NetEvolveSuppressorBase.ShouldSuppress"/>.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class NoOpSuppressor : NetEvolveSuppressorBase
    {
        private static readonly SuppressionDescriptor Suppression = new(
            id: "NES9999",
            suppressedDiagnosticId: "MA0154",
            justification: "Test-only suppressor exercising the base class's default ShouldSuppress."
        );

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
            ImmutableArray.Create(Suppression);
    }
}
