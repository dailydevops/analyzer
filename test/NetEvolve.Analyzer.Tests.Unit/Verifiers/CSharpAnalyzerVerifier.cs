namespace NetEvolve.Analyzer.Tests.Unit.Verifiers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Thin wrapper around <see cref="CSharpAnalyzerTest{TAnalyzer, TVerifier}"/> that fixes the verifier to the
/// framework-agnostic <see cref="DefaultVerifier"/> and pins the reference assemblies, so tests stay concise.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
internal static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>Creates a <see cref="DiagnosticResult"/> for the given diagnostic identifier.</summary>
    public static DiagnosticResult Diagnostic(string diagnosticId) =>
        CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>Runs the analyzer against <paramref name="source"/> and asserts the expected diagnostics.</summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the analyzer against <paramref name="source"/> compiled as a console application (rather than the
    /// default library), so a top-level <c>Main</c> is recognized by <see cref="Compilation.GetEntryPoint"/>.
    /// </summary>
    public static async Task VerifyAnalyzerAsExecutableAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.SolutionTransforms.Add(
            (solution, projectId) =>
            {
                var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
                return solution.WithProjectCompilationOptions(
                    projectId,
                    compilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
                );
            }
        );

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
