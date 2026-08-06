namespace NetEvolve.Analyzer.Tests.Integration.Style;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Abstractions;
using NetEvolve.Analyzer.Style;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for <see cref="RequireNumericLiteralSuffixSuppressor"/> (NES0002) through the real
/// <see cref="CompilationWithAnalyzers"/> pipeline. A <see cref="FakeS818Analyzer"/> stands in for
/// SonarAnalyzer.CSharp (not referenceable as a library from this project), reporting on every numeric
/// literal so both branches of <see cref="RequireNumericLiteralSuffixSuppressor.ShouldSuppress"/> are
/// exercised deterministically.
/// </summary>
public sealed class RequireNumericLiteralSuffixSuppressorTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = AnalyzerCompiler.CreateCompilation(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new RequireNumericLiteralSuffixAnalyzer(),
            new RequireNumericLiteralSuffixSuppressor(),
            new FakeS818Analyzer()
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

    private static Diagnostic SingleS818(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Single(diagnostic => string.Equals(diagnostic.Id, "S818", StringComparison.Ordinal));

    [Test]
    public async Task WrongLetterAndCase_S818IsSuppressed()
    {
        // NE0012 also reports here: the suffix's letter ('u') doesn't match the required one ('L').
        const string source = """
            public sealed class Sample
            {
                public long Value = 0u;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source).ConfigureAwait(false);

        await Assert.That(SingleS818(diagnostics).IsSuppressed).IsTrue();
    }

    [Test]
    public async Task CaseOnly_S818IsNotSuppressed()
    {
        // NE0012 does not report here: the suffix's letter ('l' -> 'L') already matches, case aside.
        const string source = """
            public sealed class Sample
            {
                public long Value = 0l;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source).ConfigureAwait(false);

        await Assert.That(SingleS818(diagnostics).IsSuppressed).IsFalse();
    }

    [Test]
    public async Task NoSuffixableTarget_S818IsNotSuppressed()
    {
        // The literal isn't converted to one of the six suffixable types, so NE0012 never applies.
        const string source = """
            public sealed class Sample
            {
                public int Value = 0;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source).ConfigureAwait(false);

        await Assert.That(SingleS818(diagnostics).IsSuppressed).IsFalse();
    }

    /// <summary>
    /// Stands in for SonarAnalyzer.CSharp's S818, reporting on every numeric literal regardless of whether
    /// it actually has a lower-case suffix — the fake only needs to exist at the location the real S818
    /// would also report, since suppression is location-based.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class FakeS818Analyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Descriptor = new(
            id: "S818",
            title: "Literal suffixes should be upper case",
            messageFormat: "Literal suffixes should be upper case",
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
            context.RegisterSyntaxNodeAction(
                syntaxContext =>
                    syntaxContext.ReportDiagnostic(Diagnostic.Create(Descriptor, syntaxContext.Node.GetLocation())),
                SyntaxKind.NumericLiteralExpression
            );
        }
    }
}
