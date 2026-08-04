namespace NetEvolve.Analyzer.Tests.Unit.Documentation;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Documentation;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>Code-fix tests for NE0007: the <c>&lt;c&gt;keyword&lt;/c&gt;</c> to <c>&lt;see langword="keyword"/&gt;</c> rewrite.</summary>
public sealed class UseLangwordCodeFixTests
{
    [Test]
    public Task TrueKeyword_RewrittenToLangword() =>
        CSharpCodeFixVerifier<UseLangwordAnalyzer, UseLangwordCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns {|NE0007:<c>true</c>|} on success.</summary>
                public bool Succeeded() => true;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Returns <see langword="true"/> on success.</summary>
                public bool Succeeded() => true;
            }
            """
        );

    [Test]
    public Task NullKeyword_RewrittenToLangword() =>
        CSharpCodeFixVerifier<UseLangwordAnalyzer, UseLangwordCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns {|NE0007:<c>null</c>|} when unset.</summary>
                public object? Get() => null;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Returns <see langword="null"/> when unset.</summary>
                public object? Get() => null;
            }
            """
        );

    [Test]
    public Task InParam_RewrittenToLangword() =>
        CSharpCodeFixVerifier<UseLangwordAnalyzer, UseLangwordCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="strict">When {|NE0007:<c>true</c>|}, validation is stricter.</param>
                public bool Check(bool strict) => strict;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Checks something.</summary>
                /// <param name="strict">When <see langword="true"/>, validation is stricter.</param>
                public bool Check(bool strict) => strict;
            }
            """
        );

    [Test]
    public Task CodeKeyword_RewrittenToLangword() =>
        CSharpCodeFixVerifier<UseLangwordAnalyzer, UseLangwordCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns {|NE0007:<code>true</code>|} on success.</summary>
                public bool Succeeded() => true;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Returns <see langword="true"/> on success.</summary>
                public bool Succeeded() => true;
            }
            """
        );

    [Test]
    public Task MultipleOccurrences_FixAllRewritesEveryOccurrence() =>
        CSharpCodeFixVerifier<UseLangwordAnalyzer, UseLangwordCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                /// <summary>Returns {|NE0007:<c>true</c>|} if it succeeded; otherwise, {|NE0007:<c>false</c>|}.</summary>
                public bool Succeeded() => true;
            }
            """,
            """
            public sealed class Sample
            {
                /// <summary>Returns <see langword="true"/> if it succeeded; otherwise, <see langword="false"/>.</summary>
                public bool Succeeded() => true;
            }
            """
        );
}
