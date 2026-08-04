namespace NetEvolve.Analyzer.Tests.Unit.Style;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetEvolve.Analyzer.Style;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>
/// Code-fix tests for NE0011: removing a <c>#region</c>/<c>#endregion</c> pair while keeping the content
/// between them intact, at both the warning and the suggestion severity.
/// </summary>
public sealed class AvoidRegionDirectivesCodeFixTests
{
    [Test]
    public Task InsideMethodBody_RemovesRegionAndEndRegion() =>
        CSharpCodeFixVerifier<AvoidRegionDirectivesAnalyzer, AvoidRegionDirectivesCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public void DoWork()
                {
                    {|NE0011:#region Logic|}
                    var x = 1;
                    #endregion
                }
            }
            """,
            """
            public sealed class Sample
            {
                public void DoWork()
                {
                    var x = 1;
                }
            }
            """
        );

    [Test]
    public Task InsideMethodBodyWithBlankLinesAroundDirectives_RemovesBlankLinesToo() =>
        CSharpCodeFixVerifier<AvoidRegionDirectivesAnalyzer, AvoidRegionDirectivesCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public void DoWork()
                {
                    {|NE0011:#region Logic|}

                    var x = 1;

                    #endregion
                }
            }
            """,
            """
            public sealed class Sample
            {
                public void DoWork()
                {
                    var x = 1;
                }
            }
            """
        );

    [Test]
    public Task WrappingWholeClassAtNamespaceLevel_RemovesRegionAndEndRegion() =>
        CSharpCodeFixVerifier<AvoidRegionDirectivesAnalyzer, AvoidRegionDirectivesCodeFixProvider>.VerifyCodeFixAsync(
            """
            namespace SampleNamespace
            {
                {|#0:#region Types|}
                public sealed class Sample
                {
                    public void DoWork() { }
                }
                #endregion
            }
            """,
            """
            namespace SampleNamespace
            {
                public sealed class Sample
                {
                    public void DoWork() { }
                }
            }
            """,
            CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>
                .Diagnostic(DiagnosticIds.NE0011)
                .WithLocation(0)
                .WithSeverity(DiagnosticSeverity.Info)
        );

    [Test]
    public Task WrappingFields_RemovesRegionAndEndRegion() =>
        CSharpCodeFixVerifier<AvoidRegionDirectivesAnalyzer, AvoidRegionDirectivesCodeFixProvider>.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                {|#0:#region Fields|}
                private int _value;
                #endregion
            }
            """,
            """
            public sealed class Sample
            {
                private int _value;
            }
            """,
            CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>
                .Diagnostic(DiagnosticIds.NE0011)
                .WithLocation(0)
                .WithSeverity(DiagnosticSeverity.Info)
        );
}
