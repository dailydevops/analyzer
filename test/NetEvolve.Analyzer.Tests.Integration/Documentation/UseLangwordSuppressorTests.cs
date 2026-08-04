namespace NetEvolve.Analyzer.Tests.Integration.Documentation;

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Documentation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for <see cref="UseLangwordSuppressor"/> (NES0001) through the real
/// <see cref="CompilationWithAnalyzers"/> pipeline. A <see cref="FakeMa0154Analyzer"/> stands in for
/// Meziantou.Analyzer (not referenceable as a library from this project) and unconditionally reports
/// <c>MA0154</c> on every <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c> element, so both the "should be suppressed"
/// and "should stay reported" paths can be exercised deterministically.
/// </summary>
public sealed class UseLangwordSuppressorTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = AnalyzerCompiler.CreateCompilation(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new UseLangwordAnalyzer(),
            new UseLangwordSuppressor(),
            new FakeMa0154Analyzer()
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

        var diagnostics = await GetDiagnosticsAsync(source).ConfigureAwait(false);
        var ma0154 = diagnostics.Single(diagnostic =>
            string.Equals(diagnostic.Id, "MA0154", System.StringComparison.Ordinal)
        );

        await Assert.That(ma0154.IsSuppressed).IsTrue();
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

        var diagnostics = await GetDiagnosticsAsync(source).ConfigureAwait(false);
        var ma0154 = diagnostics.Single(diagnostic =>
            string.Equals(diagnostic.Id, "MA0154", System.StringComparison.Ordinal)
        );

        await Assert.That(ma0154.IsSuppressed).IsFalse();
    }

    /// <summary>
    /// Stands in for Meziantou.Analyzer's MA0154: reports on every <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c>
    /// element regardless of content, so it can both agree and disagree with NE0007 on purpose.
    /// </summary>
    // RS2008: a test-only stand-in reusing the real "MA0154" id, not a shipped analyzer of this package, so
    // release tracking doesn't apply. RS1038/RS1041: this test assembly targets net10.0 and references
    // Workspaces for other tests; this fake analyzer is never packed/loaded as a real compiler extension.
#pragma warning disable RS1038, RS1041, RS2008
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class FakeMa0154Analyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Descriptor = new(
            id: "MA0154",
            title: "Use langword in XML comment",
            messageFormat: "Use langword in XML comment",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxTreeAction(treeContext =>
            {
                var root = treeContext.Tree.GetRoot(treeContext.CancellationToken);
                var elements = root.DescendantNodes(descendIntoTrivia: true)
                    .OfType<XmlElementSyntax>()
                    .Where(UseLangwordAnalyzer.IsCOrCodeElement);

                foreach (var element in elements)
                {
                    treeContext.ReportDiagnostic(Diagnostic.Create(Descriptor, element.GetLocation()));
                }
            });
        }
    }
#pragma warning restore RS1038, RS1041, RS2008
}
