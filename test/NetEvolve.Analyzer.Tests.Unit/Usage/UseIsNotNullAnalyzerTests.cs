namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="UseIsNotNullAnalyzer"/> (NE0005), driven through the verifier harness.</summary>
public sealed class UseIsNotNullAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new UseIsNotNullAnalyzer();
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

    // ---- Positive: both operand orders and both patternable shapes are flagged ---------------------------

    [Test]
    public Task ValueNotEqualsNull_ReferenceType_Reports() =>
        CSharpAnalyzerVerifier<UseIsNotNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0005:value != null|};
            }
            """
        );

    [Test]
    public Task NullNotEqualsValue_ReferenceType_Reports() =>
        CSharpAnalyzerVerifier<UseIsNotNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(string value) => {|NE0005:null != value|};
            }
            """
        );

    [Test]
    public Task NullableValueType_Reports() =>
        CSharpAnalyzerVerifier<UseIsNotNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(int? value) => {|NE0005:value != null|};
            }
            """
        );

    // ---- Negative: no equivalent / legal pattern form ---------------------------------------------------

    [Test]
    public Task UserDefinedOperator_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseIsNotNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Money
            {
                public static bool operator ==(Money left, Money right) => false;

                public static bool operator !=(Money left, Money right) => true;

                public override bool Equals(object obj) => false;

                public override int GetHashCode() => 0;

                public bool Check(Money value) => value != null;
            }
            """
        );

    [Test]
    public Task ExpressionTree_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseIsNotNullAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq.Expressions;

            public sealed class Sample
            {
                public Expression<Func<string, bool>> Expr => value => value != null;
            }
            """
        );

    [Test]
    public Task PointerOperand_NoDiagnostic() =>
        VerifyUnsafeAsync(
            """
            public sealed unsafe class Sample
            {
                public bool Check(int* value) => value != null;
            }
            """
        );

    /// <summary>Runs the analyzer with <c>AllowUnsafe</c> enabled so pointer operands compile.</summary>
    private static async Task VerifyUnsafeAsync(string source)
    {
        var test = new CSharpAnalyzerTest<UseIsNotNullAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        test.SolutionTransforms.Add(
            (solution, projectId) =>
            {
                var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
                return solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true));
            }
        );

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
