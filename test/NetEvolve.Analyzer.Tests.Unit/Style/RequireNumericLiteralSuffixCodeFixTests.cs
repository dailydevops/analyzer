namespace NetEvolve.Analyzer.Tests.Unit.Style;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Style;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>Code-fix tests for NE0012: appending or correcting a numeric literal's suffix.</summary>
public sealed class RequireNumericLiteralSuffixCodeFixTests
{
    [Test]
    [Arguments("long", "0", "0L")]
    [Arguments("double", "1.5", "1.5D")]
    [Arguments("decimal", "10", "10M")]
    [Arguments("long", "0u", "0L")]
    [Arguments("long", "0x10", "0x10L")]
    [Arguments("long", "1_000_000", "1_000_000L")]
    [Arguments("System.UInt64", "0", "0UL")]
    [Arguments("System.UInt32", "0", "0U")]
    public Task Type_MissingOrWrongSuffix_UsesCanonicalSuffix(
        string fieldType,
        string badLiteral,
        string goodLiteral
    ) =>
        CSharpCodeFixVerifier<
            RequireNumericLiteralSuffixAnalyzer,
            RequireNumericLiteralSuffixCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class Sample
            {
                public {{fieldType}} Value = {|NE0012:{{badLiteral}}|};
            }
            """,
            $$"""
            public sealed class Sample
            {
                public {{fieldType}} Value = {{goodLiteral}};
            }
            """
        );

    [Test]
    [Arguments(
        """
            public sealed class Sample
            {
                public long Value = {|NE0012:0|};
            }
            """,
        """
            public sealed class Sample
            {
                public long Value = 0L;
            }
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public void Run()
                {
                    long value = {|NE0012:0|};
                }
            }
            """,
        """
            public sealed class Sample
            {
                public void Run()
                {
                    long value = 0L;
                }
            }
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public void Accept(long value) { }

                public void Run()
                {
                    Accept({|NE0012:0|});
                }
            }
            """,
        """
            public sealed class Sample
            {
                public void Accept(long value) { }

                public void Run()
                {
                    Accept(0L);
                }
            }
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public void Run(long value)
                {
                    if (value == {|NE0012:0|})
                    {
                    }
                }
            }
            """,
        """
            public sealed class Sample
            {
                public void Run(long value)
                {
                    if (value == 0L)
                    {
                    }
                }
            }
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public long GetValue() => {|NE0012:0|};
            }
            """,
        """
            public sealed class Sample
            {
                public long GetValue() => 0L;
            }
            """
    )]
    public Task Context_MissingSuffix_AddsL(string before, string after) =>
        CSharpCodeFixVerifier<
            RequireNumericLiteralSuffixAnalyzer,
            RequireNumericLiteralSuffixCodeFixProvider
        >.VerifyCodeFixAsync(before, after);
}
