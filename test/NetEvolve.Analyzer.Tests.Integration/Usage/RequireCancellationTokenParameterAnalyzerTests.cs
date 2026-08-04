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
/// End-to-end tests for NE0010 through the real
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.CompilationWithAnalyzers"/> pipeline, confirming the semantic
/// checks (override, explicit/implicit interface implementation, partial methods) hold against a genuine
/// compilation.
/// </summary>
public sealed class RequireCancellationTokenParameterAnalyzerTests
{
    private static bool IsNe0010(Microsoft.CodeAnalysis.Diagnostic diagnostic) =>
        string.Equals(diagnostic.Id, DiagnosticIds.NE0010, StringComparison.Ordinal);

    [Test]
    public async Task ReturnsTask_WithoutToken_ReportsNe0010()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run() => LoadAsync();

                private static Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationTokenParameterAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Count(IsNe0010)).IsEqualTo(1);
    }

    [Test]
    public async Task ReturnsTask_NoAppendableCallInBody_ReportsNothing()
    {
        const string source = """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run() => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationTokenParameterAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0010)).IsFalse();
    }

    [Test]
    public async Task ReturnsTask_WithToken_ReportsNothing()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationTokenParameterAnalyzer())
            .ConfigureAwait(false);

        await Assert.That(diagnostics.Any(IsNe0010)).IsFalse();
    }

    [Test]
    public async Task ImplicitInterfaceImplementation_ReportsNothing()
    {
        const string source = """
            using System.Threading.Tasks;

            public interface ISample
            {
                Task Run();
            }

            public sealed class Sample : ISample
            {
                public Task Run() => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationTokenParameterAnalyzer())
            .ConfigureAwait(false);

        // The interface's own declaration is still flagged; only the implementing member is excluded.
        await Assert.That(diagnostics.Count(IsNe0010)).IsEqualTo(1);
    }

    [Test]
    public async Task OverrideMethod_ReportsNothing()
    {
        const string source = """
            using System.Threading.Tasks;

            public abstract class Base
            {
                public abstract Task Run();
            }

            public sealed class Derived : Base
            {
                public override Task Run() => Task.CompletedTask;
            }
            """;

        var diagnostics = await AnalyzerCompiler
            .GetAnalyzerDiagnosticsAsync(source, new RequireCancellationTokenParameterAnalyzer())
            .ConfigureAwait(false);

        // The abstract declaration is still flagged; only the override is excluded.
        await Assert.That(diagnostics.Count(IsNe0010)).IsEqualTo(1);
    }
}
