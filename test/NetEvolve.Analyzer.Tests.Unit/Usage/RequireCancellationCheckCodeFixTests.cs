namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
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

    [Test]
    public Task NestedForLoopsMissingCheck_AddsThrowIfCancellationRequestedBeforeOuterLoop() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
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
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(int rows, int columns, CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

    // ---- IsCancellationRequested + return form: collection-returning methods -----------------------------
    //
    // Only the Microsoft.CodeAnalysis.CSharp package versions this test project references for net9.0/net10.0
    // (4.14/5.6) know about C# 12 collection expressions at all; net8.0 (4.7) and every other target framework
    // (4.4, the default) predate the feature and cannot even represent LanguageVersion.CSharp12 as a value, let
    // alone parse "return [];" — so these three positive cases only compile/run where the feature actually
    // exists. The fallback path (below C# 12) is covered unconditionally further down.

#if SUPPORTS_COLLECTION_EXPRESSIONS
    [Test]
    public Task ListReturningMethod_AddsIsCancellationRequestedWithReturnEmptyCollection() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public List<int> {|NE0009:Compute|}(CancellationToken cancellationToken)
                {
                    return new List<int> { 42 };
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public List<int> Compute(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return [];
                    }

                    return new List<int> { 42 };
                }
            }
            """,
            IsCancellationRequestedKey,
            LanguageVersion.CSharp12
        );

    [Test]
    public Task ArrayReturningMethod_AddsIsCancellationRequestedWithReturnEmptyCollection() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                public int[] {|NE0009:Compute|}(CancellationToken cancellationToken)
                {
                    return new[] { 42 };
                }
            }
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                public int[] Compute(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return [];
                    }

                    return new[] { 42 };
                }
            }
            """,
            IsCancellationRequestedKey,
            LanguageVersion.CSharp12
        );

    [Test]
    public Task AsyncTaskOfListMethod_AddsIsCancellationRequestedWithReturnEmptyCollection() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public async Task<List<int>> {|NE0009:ComputeAsync|}(CancellationToken cancellationToken)
                {
                    return await LoadAsync().ConfigureAwait(false);
                }

                private static Task<List<int>> LoadAsync() => Task.FromResult(new List<int>());
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public async Task<List<int>> ComputeAsync(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return [];
                    }

                    return await LoadAsync().ConfigureAwait(false);
                }

                private static Task<List<int>> LoadAsync() => Task.FromResult(new List<int>());
            }
            """,
            IsCancellationRequestedKey,
            LanguageVersion.CSharp12
        );
#endif

    [Test]
    public Task ListReturningMethod_BelowCSharp12_AddsIsCancellationRequestedWithReturnDefault() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public List<int> {|NE0009:Compute|}(CancellationToken cancellationToken)
                {
                    return new List<int> { 42 };
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public List<int> Compute(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return default;
                    }

                    return new List<int> { 42 };
                }
            }
            """,
            IsCancellationRequestedKey,
            LanguageVersion.CSharp11
        );

    [Test]
    public Task AsyncEnumerableReturningMethod_WithoutYield_AddsIsCancellationRequestedWithReturnDefault() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public IAsyncEnumerable<int> {|NE0009:Compute|}(CancellationToken cancellationToken)
                {
                    return Factory();
                }

                private static IAsyncEnumerable<int> Factory() => null!;
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public IAsyncEnumerable<int> Compute(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return default;
                    }

                    return Factory();
                }

                private static IAsyncEnumerable<int> Factory() => null!;
            }
            """,
            IsCancellationRequestedKey
        );

    // ---- IsCancellationRequested form: iterator methods must use 'yield break;' ---------------------------

    [Test]
    public Task IteratorMethod_AddsIsCancellationRequestedWithYieldBreak() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public IEnumerable<int> {|NE0009:Compute|}(CancellationToken cancellationToken)
                {
                    yield return 42;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public IEnumerable<int> Compute(CancellationToken cancellationToken)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }

                    yield return 42;
                }
            }
            """,
            IsCancellationRequestedKey
        );

    // ---- Guard-clause detection edge cases --------------------------------------------------------------

    [Test]
    public Task BracedGuardClause_AddsThrowIfCancellationRequestedAfterGuard() =>
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
                    {
                        throw new ArgumentNullException(nameof(value));
                    }
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
                    {
                        throw new ArgumentNullException(nameof(value));
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    [Test]
    public Task ThrowViaMethodCall_IsNotAGuardClause_ChecksInsertedFirst() =>
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
                        throw GetException();
                    DoWork(value);
                }

                private static void DoWork(object value) { }

                private static Exception GetException() => new ArgumentNullException("value");
            }
            """,
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (value is null)
                        throw GetException();
                    DoWork(value);
                }

                private static void DoWork(object value) { }

                private static Exception GetException() => new ArgumentNullException("value");
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    [Test]
    public Task ThrowIfNullGuardHelper_AddsThrowIfCancellationRequestedAfterGuard() =>
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
                    ArgumentNullException.ThrowIfNull(value);
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
                    ArgumentNullException.ThrowIfNull(value);

                    cancellationToken.ThrowIfCancellationRequested();

                    DoWork(value);
                }

                private static void DoWork(object value) { }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    [Test]
    public Task QualifiedCancellationTokenType_IsRecognized() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public void {|NE0009:Run|}(System.Threading.CancellationToken cancellationToken)
                {
                    DoWork();
                }

                private static void DoWork() { }
            }
            """,
            """
            public sealed class Sample
            {
                public void Run(System.Threading.CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DoWork();
                }

                private static void DoWork() { }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    // ---- Insertion position edge cases -------------------------------------------------------------------

    [Test]
    public Task GuardOnlyBody_InsertsCheckAfterLastStatement() =>
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
                }
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
                }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    [Test]
    public Task EmptyBody_InsertsCheckIndentedOneLevelDeeper() =>
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
                }
            }
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                public void Run(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    // ---- Regression: leading trivia preceding the insertion point is not indentation ----------------------
    //
    // A statement's leading trivia is everything back to the previous token, not just its indentation. If a
    // blank line (or a comment) precedes the insertion point, that trivia must not leak into what the fix
    // treats as "indentation" - otherwise it gets baked into every line the fix generates.

    [Test]
    public Task BlankLineBeforeInsertionPoint_AddsThrowIfCancellationRequestedWithoutExtraBlankLines() =>
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

    [Test]
    public Task BlankLineBeforeInsertionPoint_AddsIsCancellationRequestedWithoutExtraBlankLines() =>
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

    [Test]
    public Task CommentBeforeInsertionPoint_AddsIsCancellationRequestedWithoutDuplicatingComment() =>
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
                    // do the work
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
                    // do the work
                    return 42;
                }
            }
            """,
            IsCancellationRequestedKey
        );

    [Test]
    public Task MultipleBlankLinesBeforeInsertionPoint_CollapsesToOneBlankLine() =>
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

    // Both guard clauses are "guard-clause-like", so the insertion point is at the very end of the statement
    // list, and the indentation is read from the last statement (the second guard clause) rather than from
    // whatever follows it. That last statement's own leading trivia carries the comment above it.
    [Test]
    public Task CommentBeforeLastGuardClause_AppendsThrowIfCancellationRequestedWithoutDuplicatingComment() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void {|NE0009:Run|}(object value, int count, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));
                    // second check
                    if (count < 0)
                        throw new ArgumentOutOfRangeException(nameof(count));
                }
            }
            """,
            """
            using System;
            using System.Threading;

            public sealed class Sample
            {
                public void Run(object value, int count, CancellationToken cancellationToken)
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));
                    // second check
                    if (count < 0)
                        throw new ArgumentOutOfRangeException(nameof(count));

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            """,
            ThrowIfCancellationRequestedKey
        );

    [Test]
    public Task XmlDocCommentAboveMethod_EmptyBody_InsertsCheckIndentedOneLevelDeeper() =>
        RequireCancellationCheckCodeFixVerifier<
            RequireCancellationCheckAnalyzer,
            RequireCancellationCheckCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;

            public sealed class Sample
            {
                /// <summary>Does nothing, yet.</summary>
                public void {|NE0009:Run|}(CancellationToken cancellationToken)
                {
                }
            }
            """,
            """
            using System.Threading;

            public sealed class Sample
            {
                /// <summary>Does nothing, yet.</summary>
                public void Run(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            """,
            ThrowIfCancellationRequestedKey
        );
}
