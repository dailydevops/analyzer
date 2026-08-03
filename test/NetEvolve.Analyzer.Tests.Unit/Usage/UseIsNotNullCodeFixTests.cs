namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Core;

/// <summary>Unit tests for <see cref="UseIsNotNullCodeFixProvider"/> (NE0005).</summary>
public sealed class UseIsNotNullCodeFixTests
{
    // ---- Happy path at the latest language version ------------------------------------------------------

    [Test]
    public Task ValueNotEqualsNull_RewritesToIsNotNull() =>
        CSharpCodeFixVerifier<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0005:value != null|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(string value) => value is not null;
            }
            """
        );

    [Test]
    public Task NullNotEqualsValue_RewritesToIsNotNull() =>
        CSharpCodeFixVerifier<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0005:null != value|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(string value) => value is not null;
            }
            """
        );

    // ---- Language-version gating ------------------------------------------------------------------------

    [Test]
    public Task CSharp9_EmitsIsNotNullPattern() =>
        PatternCodeFixVerifier<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public class Sample
            {
                public bool Check(string value)
                {
                    return {|NE0005:value != null|};
                }
            }
            """,
            """
            public class Sample
            {
                public bool Check(string value)
                {
                    return value is not null;
                }
            }
            """,
            LanguageVersion.CSharp9
        );

    [Test]
    public Task CSharp8_EmitsNegatedIsNull() =>
        PatternCodeFixVerifier<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public class Sample
            {
                public bool Check(string value)
                {
                    return {|NE0005:value != null|};
                }
            }
            """,
            """
            public class Sample
            {
                public bool Check(string value)
                {
                    return !(value is null);
                }
            }
            """,
            LanguageVersion.CSharp8
        );

    [Test]
    public Task CSharp6_BelowMinimum_ReportsButLeavesCodeUnchanged() =>
        PatternCodeFixVerifier<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            namespace Demo
            {
                public class Sample
                {
                    public bool Check(string value)
                    {
                        return {|NE0005:value != null|};
                    }
                }
            }
            """,
            """
            namespace Demo
            {
                public class Sample
                {
                    public bool Check(string value)
                    {
                        return {|NE0005:value != null|};
                    }
                }
            }
            """,
            LanguageVersion.CSharp6
        );

    // ---- A cast-wrapped null literal still selects the non-null operand (regression) --------------------

    [Test]
    public Task CastNullOperand_RewritesToOperandIsNotNull() =>
        CSharpCodeFixVerifier<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0005:value != (object)null|};
            }
            """,
            """
            public sealed class Sample
            {
                public bool Check(string value) => value is not null;
            }
            """
        );
}
