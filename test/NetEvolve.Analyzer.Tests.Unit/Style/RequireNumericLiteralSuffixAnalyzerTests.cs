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
