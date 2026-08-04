namespace NetEvolve.Analyzer.Tests.Unit.Documentation;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Documentation;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="UseLangwordAnalyzer"/> (NE0007), driven through the verifier harness.</summary>
public sealed class UseLangwordAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new UseLangwordAnalyzer();
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

    // ---- Positive: a bare keyword is flagged, across every doc-comment element, not only <summary> ----------

    [Test]
    public Task InSummary_Reports() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns {|NE0007:<c>true</c>|} if it succeeded; otherwise, {|NE0007:<c>false</c>|}.</summary>
                public bool Succeeded() => true;
            }
            """
        );

    [Test]
    public Task InParam_Reports() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="strict">When {|NE0007:<c>true</c>|}, validation is stricter.</param>
                public bool Check(bool strict) => strict;
            }
            """
        );

    [Test]
    public Task InReturns_Reports() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <returns>{|NE0007:<c>true</c>|} on success.</returns>
                public bool Check() => true;
            }
            """
        );

    [Test]
    public Task InValue_Reports() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets a value.</summary>
                /// <value>{|NE0007:<c>null</c>|} when unset.</value>
                public object? Value => null;
            }
            """
        );

    [Test]
    public Task NullKeyword_Reports() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns {|NE0007:<c>null</c>|} when unset.</summary>
                public object? Get() => null;
            }
            """
        );

    // ---- Negative: a <c> containing more than the bare keyword is left alone ----------------------------

    [Test]
    public Task ExpressionInC_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks <c>x == null</c>.</summary>
                public bool Check(object x) => x == null;
            }
            """
        );

    [Test]
    public Task MultipleWordsInC_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns the <c>true value</c>.</summary>
                public bool Check() => true;
            }
            """
        );

    // ---- Positive: contextual keywords are recognized too, not only reserved ones -----------------------

    [Test]
    public Task ContextualKeyword_Reports() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets the {|NE0007:<c>value</c>|}.</summary>
                public object? Value => null;
            }
            """
        );

    // ---- Negative: a plain identifier/word that is not a C# keyword at all --------------------------------

    [Test]
    public Task NonKeywordWord_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets the <c>result</c>.</summary>
                public object? Result => null;
            }
            """
        );

    // ---- Negative: no doc comment at all ------------------------------------------------------------------

    [Test]
    public Task NoDocComment_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseLangwordAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check() => true;
            }
            """
        );
}
