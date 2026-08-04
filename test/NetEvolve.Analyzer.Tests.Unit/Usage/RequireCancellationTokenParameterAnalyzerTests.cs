namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Unit tests for <see cref="RequireCancellationTokenParameterAnalyzer"/> (NE0010), driven through the
/// verifier harness.
/// </summary>
public sealed class RequireCancellationTokenParameterAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new RequireCancellationTokenParameterAnalyzer();
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

    // ---- Positive: every supported return type is flagged when the token parameter is missing -----------

    [Test]
    public Task ReturnsTask_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task ReturnsTaskOfT_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task<int> {|NE0010:Run|}() => Task.FromResult(0);
            }
            """
        );

    [Test]
    public Task ReturnsValueTask_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public ValueTask {|NE0010:Run|}() => default;
            }
            """
        );

    [Test]
    public Task ReturnsValueTaskOfT_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public ValueTask<int> {|NE0010:Run|}() => default;
            }
            """
        );

    [Test]
    public Task ReturnsAsyncEnumerableOfT_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public IAsyncEnumerable<int> {|NE0010:Run|}() => throw new System.NotSupportedException();
            }
            """
        );

    [Test]
    public Task InterfaceMethod_MissingToken_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public interface ISample
            {
                Task {|NE0010:Run|}();
            }
            """
        );

    // ---- Negative: a CancellationToken parameter present anywhere satisfies the rule ----------------------

    [Test]
    public Task TokenAsOnlyParameter_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task TokenAsFirstParameter_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken, int value) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task TokenAsLastParameter_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken) => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: an override's signature is fixed by the base member --------------------------------

    [Test]
    public Task OverrideMethod_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public abstract class Base
            {
                public abstract Task {|NE0010:Run|}();
            }

            public sealed class Derived : Base
            {
                public override Task Run() => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: an explicit interface implementation's signature is fixed by the interface ------------

    [Test]
    public Task ExplicitInterfaceImplementation_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public interface ISample
            {
                Task {|NE0010:Run|}();
            }

            public sealed class Sample : ISample
            {
                Task ISample.Run() => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: a method that implicitly implements an interface member is constrained by it -----------

    [Test]
    public Task ImplicitInterfaceImplementation_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public interface ISample
            {
                Task {|NE0010:Run|}();
            }

            public sealed class Sample : ISample
            {
                public Task Run() => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: synchronous methods are out of scope for this rule -------------------------------------

    [Test]
    public Task ReturnsInt_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public int Run() => 0;
            }
            """
        );

    [Test]
    public Task ReturnsVoid_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void Run() { }
            }
            """
        );

    [Test]
    public Task ReturnsArray_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public int[] Run() => System.Array.Empty<int>();
            }
            """
        );

    [Test]
    public Task ReturnsUnrelatedGenericType_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public List<int> Run() => new List<int>();
            }
            """
        );

    // ---- Negative: a local function is not a top-level method declaration ---------------------------------

    [Test]
    public Task LocalFunction_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public void Run()
                {
                    Task LocalRun() => Task.CompletedTask;
                    _ = LocalRun();
                }
            }
            """
        );

    // ---- Negative: only the implementing partial declaration is reported, not the defining one -----------

    [Test]
    public Task PartialMethod_ReportsOnce() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed partial class Sample
            {
                public partial Task Run();
            }

            public sealed partial class Sample
            {
                public partial Task {|NE0010:Run|}() => Task.CompletedTask;
            }
            """
        );

    // ---- Generated code is skipped, per convention -------------------------------------------------------

    [Test]
    public Task GeneratedCode_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            // <auto-generated/>
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run() => Task.CompletedTask;
            }
            """
        );
}
