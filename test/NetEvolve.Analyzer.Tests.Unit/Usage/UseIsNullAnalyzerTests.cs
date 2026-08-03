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

/// <summary>Unit tests for <see cref="UseIsNullAnalyzer"/> (NE0004), driven through the verifier harness.</summary>
public sealed class UseIsNullAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new UseIsNullAnalyzer();
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

    // ---- Positive: both operand orders are flagged ------------------------------------------------------

    [Test]
    public Task EqualsNull_Reports() =>
        CSharpAnalyzerVerifier<UseIsNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(object value) => {|NE0004:value == null|};
            }
            """
        );

    [Test]
    public Task NullEquals_Reports() =>
        CSharpAnalyzerVerifier<UseIsNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(object value) => {|NE0004:null == value|};
            }
            """
        );

    [Test]
    public Task NullableValueType_Reports() =>
        CSharpAnalyzerVerifier<UseIsNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(int? value) => {|NE0004:value == null|};
            }
            """
        );

    // ---- Negative: user-defined operator '==' keeps its own semantics -----------------------------------

    [Test]
    public Task UserDefinedOperator_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseIsNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(Widget widget) => widget == null;

                public sealed class Widget
                {
                    public static bool operator ==(Widget left, Widget right) => false;

                    public static bool operator !=(Widget left, Widget right) => true;
                }
            }
            """
        );

    // ---- Negative: a non-nullable value type is never null ----------------------------------------------

    [Test]
    public Task NonNullableValueType_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseIsNullAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public bool Check(int value) => value == null;
            }
            """
        );

    // ---- Negative: pointer operand has no 'is null' form ------------------------------------------------

    [Test]
    public Task PointerOperand_NoDiagnostic() =>
        VerifyUnsafeAsync(
            """
            public sealed unsafe class Sample
            {
                public bool Check(int* pointer) => pointer == null;
            }
            """
        );

    // ---- Negative: inside a LINQ expression tree the pattern does not compile ---------------------------

    [Test]
    public Task WithinExpressionTree_NoDiagnostic() =>
        CSharpAnalyzerVerifier<UseIsNullAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq.Expressions;

            public sealed class Sample
            {
                public Expression<Func<object, bool>> Check() => value => value == null;
            }
            """
        );

    /// <summary>
    /// Runs the analyzer with <c>AllowUnsafe</c> enabled, so a pointer-operand negative case compiles.
    /// </summary>
    private static async Task VerifyUnsafeAsync(string source)
    {
        var test = new CSharpAnalyzerTest<UseIsNullAnalyzer, DefaultVerifier>
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
