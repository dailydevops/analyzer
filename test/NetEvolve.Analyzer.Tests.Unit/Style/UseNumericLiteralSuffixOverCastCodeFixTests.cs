namespace NetEvolve.Analyzer.Tests.Unit.Style;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Style;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>Code-fix tests for NE0013: replacing a numeric cast with the literal's suffix.</summary>
public sealed class UseNumericLiteralSuffixOverCastCodeFixTests
{
    [Test]
    [Arguments("{|NE0013:(long)0|}", "0L")]
    [Arguments("{|NE0013:(float)1|}", "1F")]
    [Arguments("{|NE0013:(decimal)10|}", "10M")]
    [Arguments("{|NE0013:(long)0x10|}", "0x10L")]
    public Task CastToSuffixableType_ReplacedWithSuffixedLiteral(string before, string after) =>
        CSharpCodeFixVerifier<
            UseNumericLiteralSuffixOverCastAnalyzer,
            UseNumericLiteralSuffixOverCastCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class Sample
            {
                public object Value = {{before}};
            }
            """,
            $$"""
            public sealed class Sample
            {
                public object Value = {{after}};
            }
            """
        );

    [Test]
    [Arguments(
        """
            public sealed class Sample
            {
                public long Value = {|NE0013:(long)0|};
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
                    var value = {|NE0013:(long)0|};
                }
            }
            """,
        """
            public sealed class Sample
            {
                public void Run()
                {
                    var value = 0L;
                }
            }
            """
    )]
    // Regression test: an ArgumentSyntax with no ref/name-colon has the exact same span as the cast it
    // wraps, which used to make FindNode return the wrong node without getInnermostNodeForTie: true.
    [Arguments(
        """
            public sealed class Sample
            {
                public void Accept(long value) { }

                public void Run()
                {
                    Accept({|NE0013:(long)0|});
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
                    if (value == {|NE0013:(long)0|})
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
    public Task CastToLong_ReplacedWithSuffixedLiteral_AcrossContexts(string source, string fixedSource) =>
        CSharpCodeFixVerifier<
            UseNumericLiteralSuffixOverCastAnalyzer,
            UseNumericLiteralSuffixOverCastCodeFixProvider
        >.VerifyCodeFixAsync(source, fixedSource);
}
