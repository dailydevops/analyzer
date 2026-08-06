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
/// End-to-end tests for NE0009 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the guard-clause
/// walk and cancellation-check detection hold against a genuine compilation.
/// </summary>
public sealed class RequireCancellationCheckAnalyzerTests
{
    private static bool IsNe0009(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0009, StringComparison.Ordinal);

    [Test]
    public async Task MissingCheck_ReportsNe0009()
    {
        const string source = """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    DoWork();
                }

                private static void DoWork() { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0009)).IsEqualTo(1);
    }

    [Test]
    public async Task ThrowIfCancellationRequestedFirst_ReportsNothing()
    {
        const string source = """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DoWork();
                }

                private static void DoWork() { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0009)).IsFalse();
    }

    [Test]
    public async Task GuardClauseThenIsCancellationRequestedReturn_ReportsNothing()
    {
        const string source = """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0009)).IsFalse();
    }

    [Test]
    public async Task ThrowIfNullGuardHelperThenMissingCheck_ReportsNe0009()
    {
        const string source = """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, CancellationToken cancellationToken)
                {
                    ArgumentNullException.ThrowIfNull(value);

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0009)).IsEqualTo(1);
    }

    [Test]
    public async Task ExpressionBodiedMethod_ReportsNothing()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0009)).IsFalse();
    }

    [Test]
    public async Task NestedForLoopsMissingCheck_ReportsNe0009()
    {
        const string source = """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(int rows, int columns, CancellationToken cancellationToken)
                {
                    for (var row = 0; row < rows; row++)
                    {
                        for (var column = 0; column < columns; column++)
                        {
                            DoWork(row, column);
                        }
                    }
                }

                private static void DoWork(int row, int column) { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0009)).IsEqualTo(1);
    }

    [Test]
    public async Task NestedForLoopsWithLeadingCheckInInnermost_ReportsNothing()
    {
        const string source = """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(int rows, int columns, CancellationToken cancellationToken)
                {
                    for (var row = 0; row < rows; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        for (var column = 0; column < columns; column++)
                        {
                            DoWork(row, column);
                        }
                    }
                }

                private static void DoWork(int row, int column) { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0009)).IsFalse();
    }

    [Test]
    public async Task NoCancellationTokenParameter_ReportsNothing()
    {
        const string source = """
            public sealed class Sample
            {
                public void Run(object value)
                {
                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationCheckAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0009)).IsFalse();
    }
}
