namespace NetEvolve.Analyzer.Tests.Unit.Style;

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetEvolve.Analyzer.Style;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="AvoidRegionDirectivesAnalyzer"/> (NE0011), driven through the verifier harness.</summary>
public sealed class AvoidRegionDirectivesAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new AvoidRegionDirectivesAnalyzer();
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

    // ---- Warning: #region nested inside a member body -----------------------------------------------------

    [Test]
    public Task InsideMethodBody_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
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
            """
        );

    [Test]
    public Task InsideConstructorBody_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public Sample()
                {
                    {|NE0011:#region Init|}
                    var x = 1;
                    #endregion
                }
            }
            """
        );

    [Test]
    public Task InsideAccessorBody_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                private int _value;

                public int Value
                {
                    get
                    {
                        {|NE0011:#region Getter|}
                        return _value;
                        #endregion
                    }
                }
            }
            """
        );

    [Test]
    public Task InsideOperatorBody_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public static Sample operator +(Sample left, Sample right)
                {
                    {|NE0011:#region Combine|}
                    return left;
                    #endregion
                }
            }
            """
        );

    [Test]
    public Task InsideLocalFunctionBody_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void DoWork()
                {
                    Local();

                    void Local()
                    {
                        {|NE0011:#region Local|}
                        var x = 1;
                        #endregion
                    }
                }
            }
            """
        );

    [Test]
    public Task InsideNestedBlockWithinMethodBody_ReportsWarning() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void DoWork(bool flag)
                {
                    if (flag)
                    {
                        {|NE0011:#region Branch|}
                        var x = 1;
                        #endregion
                    }
                }
            }
            """
        );

    // ---- Info: #region at type level, namespace level, or file level --------------------------------------

    [Test]
    public Task WrappingWholeClassAtNamespaceLevel_ReportsInfo() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
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
            CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>
                .Diagnostic(DiagnosticIds.NE0011)
                .WithLocation(0)
                .WithSeverity(DiagnosticSeverity.Info)
        );

    [Test]
    public Task WrappingUsingDirectivesAtFileLevel_ReportsInfo() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            {|#0:#region Usings|}
            using System;
            #endregion

            public sealed class Sample
            {
                public void DoWork() => Console.WriteLine();
            }
            """,
            CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>
                .Diagnostic(DiagnosticIds.NE0011)
                .WithLocation(0)
                .WithSeverity(DiagnosticSeverity.Info)
        );

    [Test]
    public Task WrappingFields_ReportsInfo() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                {|#0:#region Fields|}
                private int _value;
                #endregion
            }
            """,
            CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>
                .Diagnostic(DiagnosticIds.NE0011)
                .WithLocation(0)
                .WithSeverity(DiagnosticSeverity.Info)
        );

    // ---- Nested regions: each reported at its own correct severity ----------------------------------------

    [Test]
    public Task NestedRegions_EachReportedAtOwnSeverity() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                {|#0:#region Members|}
                public void DoWork()
                {
                    {|NE0011:#region Logic|}
                    var x = 1;
                    #endregion
                }
                #endregion
            }
            """,
            CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>
                .Diagnostic(DiagnosticIds.NE0011)
                .WithLocation(0)
                .WithSeverity(DiagnosticSeverity.Info)
        );

    // ---- Negative: unterminated #region has no matching #endregion ----------------------------------------

    [Test]
    public Task NoDirectives_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void DoWork() { }
            }
            """
        );

    // ---- Negative: generated code is skipped -------------------------------------------------------------

    [Test]
    public Task GeneratedCode_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRegionDirectivesAnalyzer>.VerifyAnalyzerAsync(
            """
            // <auto-generated/>
            public sealed class Sample
            {
                public void DoWork()
                {
                    #region Logic
                    var x = 1;
                    #endregion
                }
            }
            """
        );
}
