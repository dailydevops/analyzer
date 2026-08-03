namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Usage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using Verifier = NetEvolve.Analyzer.Tests.Unit.Verifiers.CSharpAnalyzerVerifier<NetEvolve.Analyzer.Usage.UseIsNotNullOverIsObjectAnalyzer>;

/// <summary>Unit tests for <see cref="UseIsNotNullOverIsObjectAnalyzer"/> (NE0006), driven through the verifier harness.</summary>
public sealed class UseIsNotNullOverIsObjectAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new UseIsNotNullOverIsObjectAnalyzer();
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

    // ---- Positive: `is object` on patternable shapes is flagged ------------------------------------------

    [Test]
    public Task ReferenceType_IsObject_Reports() =>
        Verifier.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0006:value is object|};
            }
            """
        );

    [Test]
    public Task NullableValueType_IsObject_Reports() =>
        Verifier.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(int? value) => {|NE0006:value is object|};
            }
            """
        );

    // ---- Negative: no equivalent / legal pattern form ---------------------------------------------------

    [Test]
    public Task NonNullableValueType_AlwaysTrue_NoDiagnostic() =>
        Verifier.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(int value) => value is object;
            }
            """
        );

    [Test]
    public Task DeclarationPattern_NoDiagnostic() =>
        Verifier.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => value is object o && o is not null;
            }
            """
        );

    [Test]
    public Task ExpressionTree_NoDiagnostic() =>
        Verifier.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq.Expressions;

            public sealed class Sample
            {
                public Expression<Func<string, bool>> Expr => value => value is object;
            }
            """
        );
}
