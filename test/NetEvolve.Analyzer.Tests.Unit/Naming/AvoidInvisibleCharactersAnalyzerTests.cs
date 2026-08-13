namespace NetEvolve.Analyzer.Tests.Unit.Naming;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Naming;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="AvoidInvisibleCharactersAnalyzer"/> (NE0014), driven through the verifier harness.</summary>
public sealed class AvoidInvisibleCharactersAnalyzerTests
{
    // A zero-width space (U+200B) — the same category of character removed in the motivating fix.
    private const string ZeroWidthSpace = "\u200B";

    // A byte-order mark / zero-width no-break space (U+FEFF).
    private const string Bom = "\uFEFF";

    // A zero-width joiner (U+200D).
    private const string ZeroWidthJoiner = "\u200D";

    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new AvoidInvisibleCharactersAnalyzer();
        ArgumentNullException? caught = null;

        try
        {
            analyzer.Initialize(null!);
        }
        catch (ArgumentNullException exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public Task ClassNameContainsZeroWidthSpace_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class {|NE0014:My{{ZeroWidthSpace}}Class|}
            {
            }
            """
        );

    [Test]
    public Task MethodNameContainsZeroWidthJoiner_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public void {|NE0014:Do{{ZeroWidthJoiner}}Work|}() { }
            }
            """
        );

    [Test]
    public Task PropertyNameContainsBom_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public int {|NE0014:Va{{Bom}}lue|} { get; set; }
            }
            """
        );

    [Test]
    public Task ParameterNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public void DoWork(int {|NE0014:va{{ZeroWidthSpace}}lue|}) { }
            }
            """
        );

    [Test]
    public Task LocalVariableNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public void DoWork()
                {
                    var {|NE0014:va{{ZeroWidthSpace}}lue|} = 1;
                }
            }
            """
        );

    [Test]
    public Task TypeParameterContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample<{|NE0014:T{{ZeroWidthSpace}}Item|}>
            {
            }
            """
        );

    [Test]
    public Task NamespaceSegmentContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            namespace {|NE0014:My{{ZeroWidthSpace}}Project|}.Sub;

            public sealed class Sample
            {
            }
            """
        );

    [Test]
    public Task DelegateNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public delegate void {|NE0014:My{{ZeroWidthSpace}}Delegate|}();
            """
        );

    [Test]
    public Task LocalFunctionNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public void DoWork()
                {
                    void {|NE0014:Do{{ZeroWidthSpace}}Local|}() { }

                    DoLocal();
                }
            }
            """
        );

    [Test]
    public Task EventNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            using System;

            public sealed class Sample
            {
                public event EventHandler? {|NE0014:My{{ZeroWidthSpace}}Event|}
                {
                    add { }
                    remove { }
                }
            }
            """
        );

    [Test]
    public Task CatchVariableNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            using System;

            public sealed class Sample
            {
                public void DoWork()
                {
                    try
                    {
                    }
                    catch (Exception {|NE0014:e{{ZeroWidthSpace}}x|})
                    {
                        _ = ex;
                    }
                }
            }
            """
        );

    [Test]
    public Task PatternVariableNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public void DoWork(object value)
                {
                    if (value is int {|NE0014:nu{{ZeroWidthSpace}}mber|})
                    {
                        _ = number;
                    }
                }
            }
            """
        );

    [Test]
    public Task ForEachVariableNameContainsInvisibleCharacter_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void DoWork(IEnumerable<int> items)
                {
                    foreach (var {|NE0014:i{{ZeroWidthSpace}}tem|} in items)
                    {
                        _ = item;
                    }
                }
            }
            """
        );

    // ---- Stray whitespace/newline trivia (e.g. a byte-order mark left over from a bad paste) ---------------

    [Test]
    public Task StrayByteOrderMarkBeforeNamespaceKeyword_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            {|NE0014:{{Bom}}|}namespace My.Project;

            public sealed class Sample
            {
            }
            """
        );

    [Test]
    public Task StrayByteOrderMarkBeforeMemberInsideClass_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
            {|NE0014:    {{Bom}}|}public void DoWork() { }
            }
            """
        );

    [Test]
    public Task FileScopedNamespaceWithTwoCleanSegments_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            """
            namespace My.Project;

            public sealed class Sample
            {
            }
            """
        );

    [Test]
    public Task CleanIdentifiers_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void DoWork(int value)
                {
                    var local = value;
                }
            }
            """
        );

    [Test]
    public Task IdentifierWithSameCharacterTwice_ReportsDistinctCodePointOnce() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class {|NE0014:My{{ZeroWidthSpace}}Cl{{ZeroWidthSpace}}ass|}
            {
            }
            """
        );

    [Test]
    public Task IdentifierWithTwoDistinctCharacters_ReportsBoth() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class {|NE0014:My{{ZeroWidthSpace}}Cl{{ZeroWidthJoiner}}ass|}
            {
            }
            """
        );

    [Test]
    public Task GeneratedCode_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidInvisibleCharactersAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            // <auto-generated/>
            public sealed class My{{ZeroWidthSpace}}Class
            {
            }
            """
        );
}
