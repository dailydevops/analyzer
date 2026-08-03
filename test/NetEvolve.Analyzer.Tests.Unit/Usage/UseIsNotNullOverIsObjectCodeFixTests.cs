namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;
using GatedVerifier = NetEvolve.Analyzer.Tests.Unit.Usage.PatternCodeFixVerifier<
    NetEvolve.Analyzer.Usage.UseIsNotNullOverIsObjectAnalyzer,
    NetEvolve.Analyzer.Usage.UseIsNotNullOverIsObjectCodeFixProvider
>;
using Verifier = NetEvolve.Analyzer.Tests.Unit.Verifiers.CSharpCodeFixVerifier<
    NetEvolve.Analyzer.Usage.UseIsNotNullOverIsObjectAnalyzer,
    NetEvolve.Analyzer.Usage.UseIsNotNullOverIsObjectCodeFixProvider
>;

/// <summary>Unit tests for the NE0006 <c>UseIsNotNullOverIsObjectCodeFixProvider</c>.</summary>
public sealed class UseIsNotNullOverIsObjectCodeFixTests
{
    // ---- Happy path at the latest language version ------------------------------------------------------

    [Test]
    public Task IsObject_RewritesToIsNotNull() =>
        Verifier.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0006:value is object|};
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
        GatedVerifier.VerifyCodeFixAsync(
            """
            public class Sample
            {
                public bool Check(string value)
                {
                    return {|NE0006:value is object|};
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
    public Task CSharp8_BelowMinimum_ReportsButLeavesCodeUnchanged() =>
        GatedVerifier.VerifyCodeFixAsync(
            """
            namespace Demo
            {
                public class Sample
                {
                    public bool Check(string value)
                    {
                        return {|NE0006:value is object|};
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
                        return {|NE0006:value is object|};
                    }
                }
            }
            """,
            LanguageVersion.CSharp8
        );
}
