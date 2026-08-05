namespace NetEvolve.Analyzer.Tests.Unit.Documentation;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NetEvolve.Analyzer.Documentation;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="NativeTypeCrefAnalyzer"/> (NE0008), driven through the verifier harness.</summary>
public sealed class NativeTypeCrefAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new NativeTypeCrefAnalyzer();
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

    // ---- Positive: a bare native type name is flagged, across every doc-comment element -------------------

    [Test]
    public Task InSummary_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns the result as a {|NE0008:<c>string</c>|}.</summary>
                public string Value() => "";
            }
            """
        );

    [Test]
    public Task InParam_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="value">An {|NE0008:<c>int</c>|} value.</param>
                public bool Check(int value) => value > 0;
            }
            """
        );

    [Test]
    public Task InReturns_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <returns>A {|NE0008:<c>bool</c>|} result.</returns>
                public bool Check() => true;
            }
            """
        );

    [Test]
    public Task InValue_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets a value.</summary>
                /// <value>A {|NE0008:<c>double</c>|}.</value>
                public double Value => 0d;
            }
            """
        );

    // ---- Positive: <code> is recognized just like <c> ------------------------------------------------------

    [Test]
    public Task InCodeElement_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns the result as a {|NE0008:<code>string</code>|}.</summary>
                public string Value() => "";
            }
            """
        );

    // ---- Positive: every recognized native type name is flagged --------------------------------------------

    [Test]
    public Task DynamicTypeName_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>dynamic</c>|} value.</summary>
                public dynamic Value => 0;
            }
            """
        );

    [Test]
    public Task NintTypeName_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>nint</c>|} value.</summary>
                public nint Value => 0;
            }
            """
        );

    // ---- Positive: common BCL value type names are flagged just like native types --------------------------

    [Test]
    public Task GuidTypeName_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>Guid</c>|} value.</summary>
                public Guid Value => Guid.Empty;
            }
            """
        );

    [Test]
    public Task DateTimeTypeName_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>DateTime</c>|} value.</summary>
                public DateTime Value => DateTime.UtcNow;
            }
            """
        );

    [Test]
    public Task DateOnlyTypeName_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>DateOnly</c>|} value.</summary>
                public DateOnly Value => DateOnly.MinValue;
            }
            """
        );

    [Test]
    public Task TimeOnlyTypeName_Reports() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>TimeOnly</c>|} value.</summary>
                public TimeOnly Value => TimeOnly.MinValue;
            }
            """
        );

    // ---- Negative: DateOnly/TimeOnly are only recognized when the compilation actually has the type ---------
    // (they were introduced in .NET 6; a consumer targeting an older framework has no such type to cref).

    [Test]
    public async Task DateOnlyTypeName_TargetFrameworkWithoutDateOnly_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<NativeTypeCrefAnalyzer, DefaultVerifier>
        {
            TestCode = """
                public sealed class Sample
                {
                    /// <summary>Gets the <c>DateOnly</c> value.</summary>
                    public string Value => "DateOnly";
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
        };

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // ---- Negative: void is excluded, it's handled by NE0007 instead ----------------------------------------

    [Test]
    public Task VoidKeyword_NoDiagnostic() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Does nothing, returns <c>void</c>.</summary>
                public void Run() { }
            }
            """
        );

    // ---- Negative: a <c> containing more than the bare type name is left alone -----------------------------

    [Test]
    public Task ExpressionInC_NoDiagnostic() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks <c>x is string</c>.</summary>
                public bool Check(object x) => x is string;
            }
            """
        );

    [Test]
    public Task MultipleWordsInC_NoDiagnostic() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns the <c>string value</c>.</summary>
                public string Value() => "";
            }
            """
        );

    // ---- Negative: a plain identifier/word that is not a native type name at all ----------------------------

    [Test]
    public Task NonTypeWord_NoDiagnostic() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets the <c>result</c>.</summary>
                public object? Result => null;
            }
            """
        );

    // ---- Negative: keywords recognized by NE0007 are not flagged by this rule ------------------------------

    [Test]
    public Task LangwordKeyword_NoDiagnostic() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns <c>true</c> on success.</summary>
                public bool Succeeded() => true;
            }
            """
        );

    // ---- Negative: no doc comment at all ------------------------------------------------------------------

    [Test]
    public Task NoDocComment_NoDiagnostic() =>
        CSharpAnalyzerVerifier<NativeTypeCrefAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check() => true;
            }
            """
        );
}
