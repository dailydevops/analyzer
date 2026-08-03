namespace NetEvolve.Analyzer.Tests.Integration;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Validates the <see cref="AnalyzerCompiler"/> harness: it must compile real source against the running
/// framework (resolving its reference assemblies) and surface diagnostics. These are the seed tests for the
/// scaffold; rule-specific integration tests are added next to each rule's category.
/// </summary>
public sealed class AnalyzerCompilerTests
{
    [Test]
    public async Task GetCompilerDiagnostics_ValidSource_HasNoErrors()
    {
        const string source = """
            using System;

            public class Order
            {
                public DateTime CreatedAt { get; init; }
            }
            """;

        var diagnostics = AnalyzerCompiler.GetCompilerDiagnostics(source);

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsFalse();
    }

    [Test]
    public async Task GetCompilerDiagnostics_InvalidSource_ReportsCompilerError()
    {
        const string source = "public class Broken {";

        var diagnostics = AnalyzerCompiler.GetCompilerDiagnostics(source);

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
    }
}
