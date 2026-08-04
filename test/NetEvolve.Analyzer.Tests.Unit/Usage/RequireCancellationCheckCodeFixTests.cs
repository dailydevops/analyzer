namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Usage;
using TUnit.Core;

/// <summary>Code-fix tests for NE0009, covering both registered fix forms.</summary>
public sealed class RequireCancellationCheckCodeFixTests
{
    private const string ThrowIfCancellationRequestedKey = "NE0009.ThrowIfCancellationRequested";
    private const string IsCancellationRequestedKey = "NE0009.IsCancellationRequested";

    // ---- ThrowIfCancellationRequested() form -------------------------------------------------------------

    [Test]
    public Task NoGuardClauses_AddsThrowIfCancellationRequested() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(CancellationToken cancellationToken)
                {
                    DoWork();
                }

                private static void DoWork() { }
            }
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DoWork();
                }

                private static void DoWork() { }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    [Test]
    public Task GuardClause_AddsThrowIfCancellationRequestedAfterGuard() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));
                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """,
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));
                    cancellationToken.ThrowIfCancellationRequested();
                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    // ---- IsCancellationRequested + return form -----------------------------------------------------------

    [Test]
    public Task VoidMethod_AddsIsCancellationRequestedWithBareReturn() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(CancellationToken cancellationToken)
                {
                    DoWork();
                }

                private static void DoWork() { }
            }
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    DoWork();
                }

                private static void DoWork() { }
            }
            """,
            IsCancellationRequestedKey
        );

    [Test]
    public Task AsyncTaskMethod_AddsIsCancellationRequestedWithBareReturn() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public async Task {|NE0009:RunAsync|}(CancellationToken cancellationToken)
                {
                    await DoWorkAsync().ConfigureAwait(false);
                }

                private static Task DoWorkAsync() => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public async Task RunAsync(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    await DoWorkAsync().ConfigureAwait(false);
                }

                private static Task DoWorkAsync() => Task.CompletedTask;
            }
            """,
            IsCancellationRequestedKey
        );

    [Test]
    public Task MethodWithReturnValue_AddsIsCancellationRequestedWithReturnDefault() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public int {|NE0009:Compute|}(CancellationToken cancellationToken)
                {
                    return 42;
                }
            }
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                public int Compute(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return default;
                    }
                    return 42;
                }
            }
            """,
            IsCancellationRequestedKey
        );
}
