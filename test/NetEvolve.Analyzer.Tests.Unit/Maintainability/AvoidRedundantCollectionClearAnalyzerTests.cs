namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Maintainability;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <see cref="AvoidRedundantCollectionClearAnalyzer"/> (NE0015).</summary>
public sealed class AvoidRedundantCollectionClearAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new AvoidRedundantCollectionClearAnalyzer();
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

    // ---- Positive: the Clear() call is the first reference after a parameterless construction ------------

    [Test]
    public Task ListOfT_ImmediateClear_Reports() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    {|NE0015:list.Clear()|};
                    list.Add(1L);
                }
            }
            """
        );

    [Test]
    public Task ArrayList_ImmediateClear_Reports() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new ArrayList();
                    {|NE0015:list.Clear()|};
                    list.Add(1);
                }
            }
            """
        );

    [Test]
    public Task ClearIsFirstUseAfterUnrelatedStatements_Reports() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(int capacity)
                {
                    var list = new List<long>();
                    var unrelated = capacity + 1;
                    using (var scope = new Scope())
                    {
                        {|NE0015:list.Clear()|};
                        list.Add(unrelated);
                    }
                }

                private sealed class Scope : System.IDisposable
                {
                    public void Dispose() { }
                }
            }
            """
        );

    // ---- Positive: matches the reported issue's shape — a population loop runs *after* the Clear() call ----

    [Test]
    public Task PopulationLoopAfterClear_Reports() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(IEnumerator<long> reader)
                {
                    var list = new List<long>();
                    using (var scope = new Scope())
                    {
                        {|NE0015:list.Clear()|};
                        while (reader.MoveNext())
                        {
                            list.Add(reader.Current);
                        }
                    }
                }

                private sealed class Scope : System.IDisposable
                {
                    public void Dispose() { }
                }
            }
            """
        );

    // ---- Positive: target-typed 'new()' is recognized as a parameterless construction -----------------------

    [Test]
    public Task ImplicitObjectCreation_Reports() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    List<long> list = new();
                    {|NE0015:list.Clear()|};
                    list.Add(1L);
                }
            }
            """
        );

    // ---- Negative: a copy-constructor argument populates the instance at creation --------------------------

    [Test]
    public Task CopyConstructor_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(List<long> other)
                {
                    var list = new List<long>(other);
                    list.Clear();
                }
            }
            """
        );

    // ---- Negative: a non-empty collection initializer populates the instance at creation --------------------

    [Test]
    public Task CollectionInitializer_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long> { 1L, 2L };
                    list.Clear();
                }
            }
            """
        );

    // ---- Negative: a prior reference means the instance may already hold data ------------------------------

    [Test]
    public Task PriorUsage_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    list.Add(1L);
                    list.Clear();
                }
            }
            """
        );

    [Test]
    public Task PassedToMethodBeforeClear_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();
                    Populate(list);
                    list.Clear();
                }

                private static void Populate(List<long> target) => target.Add(1L);
            }
            """
        );

    // ---- Negative: a user-defined Clear() is not a collection member ---------------------------------------

    [Test]
    public Task UserDefinedNonCollectionType_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            public sealed class Sample
            {
                public void Run()
                {
                    var buffer = new Buffer();
                    buffer.Clear();
                }

                private sealed class Buffer
                {
                    public void Clear() { }
                }
            }
            """
        );

    // ---- Negative: a Collection<T> subclass overriding ClearItems() can carry a side effect -----------------

    [Test]
    public Task CollectionSubclassWithOverriddenClearItems_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.ObjectModel;

            public sealed class Sample
            {
                public void Run()
                {
                    var items = new LoggingCollection();
                    items.Clear();
                }

                private sealed class LoggingCollection : Collection<int>
                {
                    protected override void ClearItems()
                    {
                        System.Console.WriteLine("cleared");
                        base.ClearItems();
                    }
                }
            }
            """
        );

    // ---- Negative: the initializer isn't a constructor call, so nothing is known about prior state ----------

    [Test]
    public Task NonObjectCreationInitializer_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = GetList();
                    list.Clear();
                }

                private static List<long> GetList() => new();
            }
            """
        );

    // ---- Negative: a declaration inside a switch section has no enclosing BlockSyntax ------------------------

    [Test]
    public Task DeclarationInSwitchSection_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(int mode)
                {
                    switch (mode)
                    {
                        case 0:
                            var list = new List<long>();
                            list.Clear();
                            list.Add(1L);
                            break;
                    }
                }
            }
            """
        );

    // ---- Negative: Clear() inside a loop can run more than once --------------------------------------------

    [Test]
    public Task ClearInsideLoop_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(int count)
                {
                    var list = new List<long>();
                    for (var i = 0; i < count; i++)
                    {
                        list.Clear();
                        list.Add(i);
                    }
                }
            }
            """
        );

    // ---- Negative: Clear() inside a local function can run at any time -------------------------------------

    [Test]
    public Task ClearInsideLocalFunction_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run()
                {
                    var list = new List<long>();

                    void Reset()
                    {
                        list.Clear();
                    }

                    Reset();
                    list.Add(1L);
                }
            }
            """
        );

    // ---- Negative: another reference inside a lambda can be invoked before the Clear() call ----------------

    [Test]
    public Task OtherReferenceInsideLambda_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(Action<Action> register)
                {
                    var list = new List<long>();
                    list.Clear();
                    register(() => list.Add(1L));
                }
            }
            """
        );

    // ---- Negative: a goto in the enclosing block makes source order unreliable ------------------------------

    [Test]
    public Task GotoInEnclosingBlock_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(bool skip)
                {
                    var list = new List<long>();
                    if (skip)
                    {
                        goto End;
                    }
                    list.Clear();
                    list.Add(1L);
                End:
                    return;
                }
            }
            """
        );

    // ---- Negative: Clear() used as an argument, not a bare statement ---------------------------------------

    [Test]
    public Task ClearNotAStatement_NoDiagnostic() =>
        CSharpAnalyzerVerifier<AvoidRedundantCollectionClearAnalyzer>.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void Run(Action action)
                {
                    var list = new List<long>();
                    Invoke(() => list.Clear());
                }

                private static void Invoke(Action action) => action();
            }
            """
        );
}
