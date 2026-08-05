namespace NetEvolve.Analyzer.Tests.Unit.Documentation;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
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
    public Task GuidTypeName_RewrittenToCref() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>Guid</c>|} value.</summary>
                public Guid Value => Guid.Empty;
            }
            """,
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the <see cref="Guid"/> value.</summary>
                public Guid Value => Guid.Empty;
            }
            """
        );

    // ---- DateOnly/TimeOnly: safe to rewrite when the project isn't multi-targeted at all ------------------

    [Test]
    public Task DateOnlyTypeName_SingleTargeted_RewrittenToCref() =>
        CSharpCodeFixVerifier<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>.VerifyCodeFixAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>DateOnly</c>|} value.</summary>
                public DateOnly Value => DateOnly.MinValue;
            }
            """,
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the <see cref="DateOnly"/> value.</summary>
                public DateOnly Value => DateOnly.MinValue;
            }
            """
        );

    // ---- DateOnly/TimeOnly: withheld when a sibling target framework in the same project has no such type --

    [Test]
    public Task DateOnlyTypeName_MultiTargetedWithIncompatibleFramework_NotRewritten() =>
        VerifyWithTargetFrameworksAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>DateOnly</c>|} value.</summary>
                public DateOnly Value => DateOnly.MinValue;
            }
            """,
            "net8.0,netstandard2.0"
        );

    [Test]
    public Task TimeOnlyTypeName_MultiTargetedWithIncompatibleFramework_NotRewritten() =>
        VerifyWithTargetFrameworksAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>TimeOnly</c>|} value.</summary>
                public TimeOnly Value => TimeOnly.MinValue;
            }
            """,
            "net8.0,net472"
        );

    [Test]
    public Task DateOnlyTypeName_MultiTargetedWithOnlyCompatibleFrameworks_RewrittenToCref() =>
        VerifyWithTargetFrameworksAsync(
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the {|NE0008:<c>DateOnly</c>|} value.</summary>
                public DateOnly Value => DateOnly.MinValue;
            }
            """,
            "net8.0;net9.0",
            """
            using System;

            public sealed class Sample
            {
                /// <summary>Gets the <see cref="DateOnly"/> value.</summary>
                public DateOnly Value => DateOnly.MinValue;
            }
            """
        );

    // Both the analyzer diagnostic (via {|NE0008:...|}) and code fix are driven from the same TestState here,
    // because the CSharpCodeFixVerifier wrapper has no hook for injecting build properties. When fixedSource
    // is omitted the fix must leave the source untouched — mirroring the "no code action available" outcome
    // CSharpCodeFixTest accepts when a diagnostic fires but the provider withholds its fix.
    //
    // targetFrameworksCsv is comma-separated, matching what build/NetEvolve.Analyzer.props actually produces
    // (see BuildProperty.TargetFrameworks) — MSBuild's own semicolon-separated $(TargetFrameworks) can't be
    // exposed as-is, since Roslyn's AnalyzerConfig format treats an unescaped ';' as a comment leader.
    private static async Task VerifyWithTargetFrameworksAsync(
        string source,
        string targetFrameworksCsv,
        string? fixedSource = null
    )
    {
        var test = new CSharpCodeFixTest<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource ?? source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        var config = new StringBuilder("is_global = true\n")
            .Append("build_property.NetEvolveAnalyzerTargetFrameworks = ")
            .Append(targetFrameworksCsv)
            .Append('\n')
            .ToString();
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", config));

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }

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
