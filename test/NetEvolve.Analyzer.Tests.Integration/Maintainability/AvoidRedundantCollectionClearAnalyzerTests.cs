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
/// End-to-end tests for NE0015 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the
/// semantic guards (collection type, copy-constructor/initializer, first-use ordering) hold against a
/// genuine compilation.
/// </summary>
public sealed class AvoidRedundantCollectionClearAnalyzerTests
{
    private static bool IsNe0015(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0015, StringComparison.Ordinal);

    [Test]
    public async Task ImmediateClearOnFreshList_ReportsNe0015()
    {
        const string source = """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    list.Clear();
                    list.Add(1L);
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRedundantCollectionClearAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0015)).IsEqualTo(1);
    }

    [Test]
    public async Task PopulationLoopAfterClear_ReportsNe0015()
    {
        const string source = """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(IEnumerator<long> reader)
                {
                    var list = new List<long>();
                    using (var scope = new Scope())
                    {
                        list.Clear();
                        while (reader.MoveNext())
                        {
                            list.Add(reader.Current);
                        }
                    }
                }

                private sealed class Scope : System.IDisposable
                {
                    public void Dispose() { }
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRedundantCollectionClearAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0015)).IsEqualTo(1);
    }

    [Test]
    public async Task CollectionSubclassWithOverriddenClearItems_ReportsNothing()
    {
        const string source = """
            using System.Collections.ObjectModel;

            public sealed class Sample
            {
                public void Run()
                {
                    var items = new LoggingCollection();
                    items.Clear();
                }

                private sealed class LoggingCollection : Collection<int>
                {
                    protected override void ClearItems()
                    {
                        System.Console.WriteLine("cleared");
                        base.ClearItems();
                    }
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRedundantCollectionClearAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0015)).IsFalse();
    }

    [Test]
    public async Task CopyConstructor_ReportsNothing()
    {
        const string source = """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(List<long> other)
                {
                    var list = new List<long>(other);
                    list.Clear();
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRedundantCollectionClearAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0015)).IsFalse();
    }

    [Test]
    public async Task PriorUsage_ReportsNothing()
    {
        const string source = """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    list.Add(1L);
                    list.Clear();
                }
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new AvoidRedundantCollectionClearAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0015)).IsFalse();
    }
}
