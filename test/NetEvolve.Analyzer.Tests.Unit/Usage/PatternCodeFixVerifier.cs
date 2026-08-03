namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Code-fix verifier for the null-check idiom rules that pins the project's C# <see cref="LanguageVersion"/>, so
/// tests can exercise the language-version gating of the fixes: below the minimum version the diagnostic is
/// still reported but the fix leaves the code unchanged (pass <c>fixedSource</c> equal to the
/// source), while at or above it the pattern rewrite is applied.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer producing the diagnostics.</typeparam>
/// <typeparam name="TCodeFix">The code fix under test.</typeparam>
internal static class PatternCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>Creates a <see cref="DiagnosticResult"/> for the given diagnostic identifier.</summary>
    public static DiagnosticResult Diagnostic(string diagnosticId) =>
        CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>Applies the code fix at the given <paramref name="languageVersion"/> and asserts the result.</summary>
    public static async Task VerifyCodeFixAsync(
        string source,
        string fixedSource,
        LanguageVersion languageVersion,
        params DiagnosticResult[] expected
    )
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.SolutionTransforms.Add(
            (solution, projectId) =>
            {
                var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
                return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
            }
        );

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
