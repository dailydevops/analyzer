namespace NetEvolve.Analyzer.Tests.Unit.Style;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Style;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="RequireNumericLiteralSuffixAnalyzer"/> (NE0012).</summary>
public sealed class RequireNumericLiteralSuffixAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new RequireNumericLiteralSuffixAnalyzer();
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

    // ---- Type axis: missing suffix, one per suffixable type, field context ---------------------------------

    [Test]
    [Arguments("long", "0")]
    [Arguments("ulong", "0")]
    [Arguments("uint", "0")]
    [Arguments("float", "1")]
    [Arguments("double", "1.5")]
    [Arguments("decimal", "10")]
    public Task MissingSuffix_PerType_ReportsDiagnostic(string type, string literal) =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            FieldWithDiagnostic(type, literal)
        );

    [Test]
    public Task LongAssignment_WrongSuffix_ReportsDiagnostic() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public long Value = {|NE0012:0u|};
            }
            """
        );

    // ---- Context axis: missing suffix, fixed type (long/0), one row per surrounding context ----------------

    [Test]
    [Arguments(
        """
            public sealed class Sample
            {
                public void Method()
                {
                    long value = {|NE0012:0|};
                }
            }
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public void Accept(long value) { }

                public void Call() => Accept({|NE0012:0|});
            }
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public bool Check(long value)
                {
                    if (value == {|NE0012:0|})
                    {
                        return true;
                    }

                    return false;
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
            """
    )]
    [Arguments(
        """
            public sealed class Sample
            {
                public long? Value = {|NE0012:0|};
            }
            """
    )]
    public Task MissingSuffix_VariousContexts_ReportsDiagnostic(string source) =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(source);

    // ---- Overload resolution: a literal's type resolved via overload resolution among sibling overloads with
    // different suffixable numeric types accepts any of those types' suffixes — that overload set (and thus
    // the picked type) can differ between .NET versions, so forcing exactly one suffix here would flip-flop
    // across a multi-targeted project's target frameworks (e.g. TimeSpan.FromMinutes gained a 'long' overload
    // in .NET 9 alongside its pre-existing 'double' one; a bare '5' needs 'D' on net8.0 but 'L' on net9.0+).
    // No suffix at all is still flagged, since none of the accepted suffixes were used. A single, unambiguous
    // overload is unaffected and still requires exactly its canonical suffix. -----------------------------------

    [Test]
    public Task OverloadResolution_LongAndDoubleOverloads_NoSuffix_ReportsDiagnostic() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void Accept(long number) { }

                public void Accept(double number) { }

                public void Call() => Accept({|NE0012:0|});
            }
            """
        );

    [Test]
    [Arguments("0L")]
    [Arguments("0D")]
    public Task OverloadResolution_LongAndDoubleOverloads_EitherAcceptedSuffix_ReportsNothing(string literal) =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            $$"""
            public sealed class Sample
            {
                public void Accept(long number) { }

                public void Accept(double number) { }

                public void Call() => Accept({{literal}});
            }
            """
        );

    [Test]
    public Task OverloadResolution_SingleOverload_StillReportsDiagnostic() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void Accept(long number) { }

                public void Call() => Accept({|NE0012:0|});
            }
            """
        );

    // ---- Real-world: TimeSpan.FromMinutes gained a 'long' overload in .NET 9 alongside the pre-existing
    // 'double' one (net8.0/netstandard2.0 still only have 'double'). An explicit 'D' suffix forces the
    // 'double' overload — and is already correctly suffixed for it — on every target framework, so it's the
    // one portable fix. This test runs against all three of this project's TargetFrameworks
    // (net8.0/net9.0/net10.0), proving 'D' never flags on any of them.

    [Test]
    public Task TimeSpanFromMinutes_ExplicitDoubleSuffix_ReportsNothing() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Sample
            {
                public TimeSpan Value = TimeSpan.FromMinutes(5D);
            }
            """
        );

    // ---- Negative: already correctly suffixed --------------------------------------------------------------

    [Test]
    [Arguments("long", "0L")]
    [Arguments("decimal", "10M")]
    public Task CorrectSuffix_ReportsNothing(string type, string literalWithSuffix) =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            FieldWithoutDiagnostic(type, literalWithSuffix)
        );

    // ---- Negative: int has no suffix to require ------------------------------------------------------------

    [Test]
    public Task IntAssignment_ReportsNothing() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public int Value = 0;
            }
            """
        );

    // ---- Negative: hex/binary literal converted to a floating/decimal type can't be marked -----------------

    [Test]
    public Task HexLiteral_ConvertedToDouble_ReportsNothing() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public double Value = 0x10;
            }
            """
        );

    [Test]
    public Task HexLiteral_ConvertedToLong_MissingSuffix_ReportsDiagnostic() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public long Value = {|NE0012:0x10|};
            }
            """
        );

    // ---- Negative: generated code is skipped -----------------------------------------------------------------

    [Test]
    public Task GeneratedCode_ReportsNothing() =>
        CSharpAnalyzerVerifier<RequireNumericLiteralSuffixAnalyzer>.VerifyAnalyzerAsync(
            """
            // <auto-generated/>
            public sealed class Sample
            {
                public long Value = 0;
            }
            """
        );

    private static string FieldWithDiagnostic(string type, string literal) =>
        $$"""
            public sealed class Sample
            {
                public {{type}} Value = {|NE0012:{{literal}}|};
            }
            """;

    private static string FieldWithoutDiagnostic(string type, string literal) =>
        $$"""
            public sealed class Sample
            {
                public {{type}} Value = {{literal}};
            }
            """;
}
