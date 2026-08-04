namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Code-fix verifier for NE0009 that pins which of the two registered code actions (identified by its
/// equivalence key) gets applied, since the NE0009 code fix always offers both forms for the same
/// diagnostic.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer producing the diagnostics.</typeparam>
/// <typeparam name="TCodeFix">The code fix under test.</typeparam>
internal static class RequireCancellationCheckCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>Applies the code action identified by <paramref name="codeActionEquivalenceKey"/> and asserts the result.</summary>
    public static async Task VerifyCodeFixAsync(
        string source,
        string fixedSource,
        string codeActionEquivalenceKey,
        params DiagnosticResult[] expected
    )
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionEquivalenceKey = codeActionEquivalenceKey,
        };

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
