namespace NetEvolve.Analyzer.Tests.Unit.Verifiers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Thin wrapper around <see cref="CSharpCodeFixTest{TAnalyzer, TCodeFix, TVerifier}"/> that fixes the verifier
/// to the framework-agnostic <see cref="DefaultVerifier"/> and pins the reference assemblies.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer producing the diagnostics.</typeparam>
/// <typeparam name="TCodeFix">The code fix under test.</typeparam>
internal static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>Creates a <see cref="DiagnosticResult"/> for the given diagnostic identifier.</summary>
    public static DiagnosticResult Diagnostic(string diagnosticId) =>
        CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>Applies the code fix to <paramref name="source"/> and asserts the result equals <paramref name="fixedSource"/>.</summary>
    public static Task VerifyCodeFixAsync(string source, string fixedSource, params DiagnosticResult[] expected) =>
        VerifyCodeFixAsync(source, fixedSource, codeActionIndex: null, expected);

    /// <summary>
    /// Applies the code fix at <paramref name="codeActionIndex"/> to <paramref name="source"/> and asserts the
    /// result equals <paramref name="fixedSource"/>. Use when the fix registers more than one code action for
    /// the same diagnostic (e.g. an ambiguous-overload suggestion offering several suffixes). The Fix All
    /// check is skipped whenever an index is given — those actions carry no equivalence key on purpose (no
    /// Fix All support), so the framework's default "Fix All reproduces the same result" assumption doesn't
    /// apply.
    /// </summary>
    public static async Task VerifyCodeFixAsync(
        string source,
        string fixedSource,
        int? codeActionIndex,
        params DiagnosticResult[] expected
    )
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CodeActionIndex = codeActionIndex,
            CodeFixTestBehaviors = codeActionIndex is null
                ? CodeFixTestBehaviors.None
                : CodeFixTestBehaviors.SkipFixAllCheck,
        };

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
