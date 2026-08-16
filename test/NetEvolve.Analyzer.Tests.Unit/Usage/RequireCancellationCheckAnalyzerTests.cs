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
    public Task NonExceptionThrow_IsNotAGuardClause_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                        return;

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """
        );

    [Test]
    public Task NonArgumentExceptionThrow_IsAGuardClause_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new InvalidOperationException();

                    cancellationToken.ThrowIfCancellationRequested();

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """
        );

    // ---- Negative: a leading loop whose own first statement is the check satisfies the rule --------------

    [Test]
    public Task ForLoopFirstStatementThrowIfCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(int count, CancellationToken cancellationToken)
                {
                    for (var i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        DoWork(i);
                    }
                }

                private static void DoWork(int value) { }
            }
            """
        );

    [Test]
    public Task ForEachLoopFirstStatementIsCancellationRequestedReturn_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(IEnumerable<int> items, CancellationToken cancellationToken)
                {
                    foreach (var item in items)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        DoWork(item);
                    }
                }

                private static void DoWork(int value) { }
            }
            """
        );

    [Test]
    public Task WhileLoopSingleStatementBodyThrowIfCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    while (true)
                        cancellationToken.ThrowIfCancellationRequested();
                }
            }
            """
        );

    [Test]
    public Task WhileLoopConditionNegatedIsCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        DoWork();
                    }
                }

                private static void DoWork() { }
            }
            """
        );

    [Test]
    public Task GuardClause_ThenDoLoopFirstStatementThrowIfCancellationRequested_NoDiagnostic() =>
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

                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        DoWork(value);
                    } while (false);
                }

                private static void DoWork(object value) { }
            }
            """
        );

    [Test]
    public Task ThrowIfNullOrEmptyGuardHelper_ThenForLoopFirstStatementThrowIfCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(string text, int count, CancellationToken cancellationToken)
                {
                    ArgumentException.ThrowIfNullOrEmpty(text);

                    for (var i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        DoWork(i);
                    }
                }

                private static void DoWork(int value) { }
            }
            """
        );

    // ---- Negative: nested loops — the check may sit at any nesting depth, as long as it leads ------------

    [Test]
    public Task NestedForLoops_InnermostLoopFirstStatementThrowIfCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(int rows, int columns, CancellationToken cancellationToken)
                {
                    for (var row = 0; row < rows; row++)
                    {
                        for (var column = 0; column < columns; column++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            DoWork(row, column);
                        }
                    }
                }

                private static void DoWork(int row, int column) { }
            }
            """
        );

    [Test]
    public Task ForEachVariableLoopFirstStatementThrowIfCancellationRequested_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(IEnumerable<(int Row, int Column)> items, CancellationToken cancellationToken)
                {
                    foreach (var (row, column) in items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        DoWork(row, column);
                    }
                }

                private static void DoWork(int row, int column) { }
            }
            """
        );

    // ---- Positive: a loop whose first statement is not the check is still flagged -------------------------

    [Test]
    public Task ForLoopEmptyBody_MissingCheck_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(int count, CancellationToken cancellationToken)
                {
                    for (var i = 0; i < count; i++)
                    {
                    }
                }
            }
            """
        );

    [Test]
    public Task ForLoopFirstStatementMissingCheck_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(int count, CancellationToken cancellationToken)
                {
                    for (var i = 0; i < count; i++)
                    {
                        DoWork(i);

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                private static void DoWork(int value) { }
            }
            """
        );

    // ---- Positive: only the leading statement is examined — a check in a later sibling loop does not count --

    [Test]
    public Task FirstLoopMissingCheck_SecondLoopHasCheck_StillReports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(int firstCount, int secondCount, CancellationToken cancellationToken)
                {
                    for (var i = 0; i < firstCount; i++)
                    {
                        DoWork(i);
                    }

                    for (var i = 0; i < secondCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        DoWork(i);
                    }
                }

                private static void DoWork(int value) { }
            }
            """
        );

    [Test]
    public Task NestedForLoops_CheckMissingEntirely_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(int rows, int columns, CancellationToken cancellationToken)
                {
                    for (var row = 0; row < rows; row++)
                    {
                        for (var column = 0; column < columns; column++)
                        {
                            DoWork(row, column);
                        }
                    }
                }

                private static void DoWork(int row, int column) { }
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

    // ---- Local functions are inspected the same way as methods -------------------------------------------

    [Test]
    public Task LocalFunction_NoGuardClauses_MissingCheck_Reports() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task RunAsync()
                {
                    return TestMethod(default);

                    static Task {|NE0009:TestMethod|}(CancellationToken cancellationToken)
                    {
                        return Task.Delay(1000);
                    }
                }
            }
            """
        );

    [Test]
    public Task LocalFunction_ThrowIfCancellationRequestedFirst_NoDiagnostic() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task RunAsync()
                {
                    return TestMethod(default);

                    static Task TestMethod(CancellationToken cancellationToken)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        return Task.Delay(1000);
                    }
                }
            }
            """
        );

    [Test]
    public Task LocalFunction_ExpressionBodied_NotFlagged() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task RunAsync()
                {
                    return TestMethod(default);

                    static Task TestMethod(CancellationToken cancellationToken) => Task.Delay(1000);
                }
            }
            """
        );

    [Test]
    public Task MethodAndNestedLocalFunction_BothMissingCheck_ReportsBoth() =>
        CSharpAnalyzerVerifier<RequireCancellationCheckAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0009:RunAsync|}(CancellationToken cancellationToken)
                {
                    return TestMethod(cancellationToken);

                    static Task {|NE0009:TestMethod|}(CancellationToken cancellationToken)
                    {
                        return Task.Delay(1000);
                    }
                }
            }
            """
        );
}
