namespace NetEvolve.Analyzer.Tests.Unit.Documentation;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Documentation;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>Code-fix tests for NE0008: the <c>&lt;c&gt;type&lt;/c&gt;</c> to <c>&lt;see cref="type"/&gt;</c> rewrite.</summary>
public sealed class NativeTypeCrefCodeFixTests
{
    [Test]
    public Task StringTypeName_RewrittenToCref() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns the result as a {|NE0008:<c>string</c>|}.</summary>
                public string Value() => "";
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Returns the result as a <see cref="string"/>.</summary>
                public string Value() => "";
            }
            """
        );

    [Test]
    public Task IntTypeName_RewrittenToCref() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>int</c>|} value.</summary>
                public int Value => 0;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Gets the <see cref="int"/> value.</summary>
                public int Value => 0;
            }
            """
        );

    [Test]
    public Task InParam_RewrittenToCref() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="value">An {|NE0008:<c>int</c>|} value.</param>
                public bool Check(int value) => value > 0;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="value">An <see cref="int"/> value.</param>
                public bool Check(int value) => value > 0;
            }
            """
        );

    [Test]
    public Task CodeElement_RewrittenToCref() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns the result as a {|NE0008:<code>string</code>|}.</summary>
                public string Value() => "";
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Returns the result as a <see cref="string"/>.</summary>
                public string Value() => "";
            }
            """
        );

    [Test]
    public Task MultipleOccurrences_FixAllRewritesEveryOccurrence() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Takes an {|NE0008:<c>int</c>|} and returns a {|NE0008:<c>string</c>|}.</summary>
                public string Format(int value) => value.ToString();
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Takes an <see cref="int"/> and returns a <see cref="string"/>.</summary>
                public string Format(int value) => value.ToString();
            }
            """
        );
}
