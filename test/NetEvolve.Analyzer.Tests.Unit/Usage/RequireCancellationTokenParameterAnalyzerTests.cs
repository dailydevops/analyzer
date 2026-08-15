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

    // ---- Positive: every supported return type is flagged when the token parameter is missing, given a body
    // ---- that has somewhere to pass the token on to (see the "body eligibility" section further down) -------

    [Test]
    public Task ReturnsTask_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => LoadAsync();

                private static Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task ReturnsTaskOfT_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task<int> {|NE0010:Run|}() => LoadAsync();

                private static Task<int> LoadAsync(CancellationToken cancellationToken = default) =>
                    Task.FromResult(0);
            }
            """
        );

    [Test]
    public Task ReturnsValueTask_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public ValueTask {|NE0010:Run|}() => LoadAsync();

                private static ValueTask LoadAsync(CancellationToken cancellationToken = default) => default;
            }
            """
        );

    [Test]
    public Task ReturnsValueTaskOfT_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public ValueTask<int> {|NE0010:Run|}() => LoadAsync();

                private static ValueTask<int> LoadAsync(CancellationToken cancellationToken = default) => default;
            }
            """
        );

    [Test]
    public Task ReturnsAsyncEnumerableOfT_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public IAsyncEnumerable<int> {|NE0010:Run|}() => LoadAsync();

                private static IAsyncEnumerable<int> LoadAsync(CancellationToken cancellationToken = default) =>
                    throw new System.NotSupportedException();
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
            using System.Threading;
            using System.Threading.Tasks;

            public sealed partial class Sample
            {
                public partial Task Run();
            }

            public sealed partial class Sample
            {
                public partial Task {|NE0010:Run|}() => LoadAsync();

                private static Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """
        );

    // ---- Body eligibility: a method with a body is only flagged when the body has somewhere to actually pass
    // ---- the new parameter on to; otherwise the code fix would add a parameter that stays unused ------------

    [Test]
    public Task BodyHasNoAppendableCall_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run() => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: a call already wired with a genuine token (not CancellationToken.None) from an ambient
    // ---- source needs no parameter of its own to be useful ---------------------------------------------------

    [Test]
    public Task CallAlreadyPassingAmbientToken_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run() => Task.Delay(25, Ambient.CancellationToken);
            }

            public static class Ambient
            {
                public static CancellationToken CancellationToken => default;
            }
            """
        );

    // ---- Positive: a local function that itself still needs a token (because ITS body has an appendable call)
    // ---- is reason enough to flag the enclosing method, since the code fix can extend both -------------------

    [Test]
    public Task AppendableCallOnlyInsideLocalFunctionNeedingItsOwnToken_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}()
                {
                    return Local();

                    Task Local() => LoadAsync();
                }

                private static Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: a local function that already declares its own CancellationToken parameter needs no
    // ---- further help, so it isn't a reason to flag the enclosing method on its own -------------------------

    [Test]
    public Task LocalFunctionAlreadyHasOwnCancellationToken_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run()
                {
                    return Local(default);

                    static Task Local(CancellationToken token) => LoadAsync(token);
                }

                private static Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: a local function whose return type NE0010 doesn't cover isn't a reason to flag the
    // ---- enclosing method either ------------------------------------------------------------------------------

    [Test]
    public Task LocalFunctionReturnsUnsupportedType_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run()
                {
                    Local();
                    return Task.CompletedTask;

                    void Local() => LoadAsync();
                }

                private static Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task AbstractMethod_NoBody_AlwaysReports() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public abstract class Sample
            {
                public abstract Task {|NE0010:Run|}();
            }
            """
        );

    // ---- Negative: the compilation's entry point is fixed by the CLR/host -----------------------------------

    [Test]
    public Task StaticMainReturningTask_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsExecutableAsync(
            """
            using System.Threading.Tasks;

            public static class Program
            {
                public static Task Main() => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task TopLevelStatements_SynthesizedMain_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationTokenParameterAnalyzer>.VerifyAnalyzerAsExecutableAsync(
            "await System.Threading.Tasks.Task.CompletedTask;"
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
