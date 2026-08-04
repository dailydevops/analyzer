namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="RequireCancellationCheckAnalyzer"/> (NE0009).</summary>
public sealed class RequireCancellationCheckAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new RequireCancellationCheckAnalyzer();
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

    // ---- Negative: no guard clauses, check must be the very first statement -----------------------------

    [Test]
    public Task NoGuardClauses_ThrowIfCancellationRequestedFirst_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            """
        );

    [Test]
    public Task NoGuardClauses_IsCancellationRequestedReturnFirst_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
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
                }
            }
            """
        );

    // ---- Negative: one guard clause before the check --------------------------------------------------

    [Test]
    public Task OneGuardClause_ThenThrowIfCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
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
                }
            }
            """
        );

    // ---- Negative: a static ThrowIfXxx guard-helper before the check ------------------------------------

    [Test]
    public Task ThrowIfNullGuardHelper_ThenIsCancellationRequestedReturn_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, CancellationToken cancellationToken)
                {
                    ArgumentNullException.ThrowIfNull(value);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
            """
        );

    // ---- Negative: checking against any one of multiple tokens satisfies the rule ----------------------

    [Test]
    public Task MultipleCancellationTokenParameters_CheckAgainstSecond_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken first, CancellationToken second)
                {
                    second.ThrowIfCancellationRequested();
                }
            }
            """
        );

    // ---- Negative: expression-bodied methods cannot structurally hold a guard, so are skipped ----------

    [Test]
    public Task ExpressionBodiedMethod_NotFlagged() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            }
            """
        );

    // ---- Negative: no CancellationToken parameter -------------------------------------------------------

    [Test]
    public Task NoCancellationTokenParameter_NotFlagged() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void Run(object value)
                {
                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """
        );

    // ---- Positive: missing check is flagged --------------------------------------------------------------

    [Test]
    public Task NoGuardClauses_MissingCheck_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
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
            """
        );

    [Test]
    public Task GuardClause_ThenMissingCheck_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
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
            """
        );

    [Test]
    public Task AllStatementsAreGuardClauses_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));
                }
            }
            """
        );

    [Test]
    public Task NonArgumentExceptionThrow_IsNotAGuardClause_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new InvalidOperationException();

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """
        );

    [Test]
    public Task IsCancellationRequestedLookalikeOnNonTokenType_NoMatch_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public struct FakeToken
            {
                public bool IsCancellationRequested;
            }

            public sealed class Sample
            {
                public void {|NE0009:Run|}(CancellationToken cancellationToken, FakeToken fake)
                {
                    if (fake.IsCancellationRequested)
                    {
                        return;
                    }

                    DoWork();
                }

                private static void DoWork() { }
            }
            """
        );
}
