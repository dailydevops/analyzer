namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Core;

/// <summary>Code-fix tests for NE0004: the <c>== null</c> to <c>is null</c> rewrite and its language gating.</summary>
public sealed class UseIsNullCodeFixTests
{
    // ---- Happy path at the latest language version ------------------------------------------------------

    [Test]
    public Task EqualsNull_RewrittenToIsNull() =>
        CSharpCodeFixVerifier<UseIsNullAnalyzer, UseIsNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(object value) => {|NE0004:value == null|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(object value) => value is null;
            }
            """
        );

    [Test]
    public Task NullEquals_RewrittenToIsNull() =>
        CSharpCodeFixVerifier<UseIsNullAnalyzer, UseIsNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(object value) => {|NE0004:null == value|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(object value) => value is null;
            }
            """
        );

    // ---- A cast-wrapped null literal still selects the non-null operand (regression) --------------------

    [Test]
    public Task CastNullOnLeft_RewritesToOperandIsNull() =>
        CSharpCodeFixVerifier<UseIsNullAnalyzer, UseIsNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(object value) => {|NE0004:(object)null == value|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(object value) => value is null;
            }
            """
        );

    // ---- At the minimum language version (C# 7.0) the rewrite applies -----------------------------------

    [Test]
    public Task AtMinimumLanguageVersion_RewriteApplied() =>
        PatternCodeFixVerifier<UseIsNullAnalyzer, UseIsNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(object value) => {|NE0004:value == null|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(object value) => value is null;
            }
            """,
            LanguageVersion.CSharp7
        );

    // ---- Below the minimum (C# 6) the diagnostic reports but the code is unchanged ----------------------

    [Test]
    public Task BelowMinimumLanguageVersion_DiagnosticReportedButCodeUnchanged()
    {
        const string source = """
            namespace Sample
            {
                public class Widget
                {
                    public bool Check(object value)
                    {
                        return {|NE0004:value == null|};
                    }
                }
            }
            """;

        return PatternCodeFixVerifier<UseIsNullAnalyzer, UseIsNullCodeFixProvider>.VerifyCodeFixAsync(
            source,
            source,
            LanguageVersion.CSharp6
        );
    }
}
