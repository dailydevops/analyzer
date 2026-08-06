namespace NetEvolve.Analyzer.Tests.Unit.Usage;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Core;

/// <summary>
/// Code-fix tests for NE0010: appending a <c>CancellationToken</c> parameter and adding the missing
/// <c>using System.Threading;</c> directive.
/// </summary>
public sealed class RequireCancellationTokenParameterCodeFixTests
{
    [Test]
    public Task NoParameters_AppendsTokenAndAddsUsing() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => HelperAsync();

                private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken = default) => HelperAsync(token: cancellationToken);

                private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task FlaggedMethodHasParamsParameter_InsertsTokenBeforeParams() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public abstract class Sample
            {
                public abstract Task {|NE0010:Run|}(params int[] values);
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public abstract class Sample
            {
                public abstract Task Run(CancellationToken cancellationToken = default, params int[] values);
            }
            """
        );

    [Test]
    public Task ExistingParameters_AppendsTokenLast() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value) => HelperAsync(value);

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default) => HelperAsync(value, token: cancellationToken);

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task UsingAlreadyPresent_DoesNotDuplicateUsing() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => HelperAsync();

                private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken = default) => HelperAsync(token: cancellationToken);

                private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task FileScopedNamespace_UsingInsertedAtNamespaceLevel() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            namespace Sample;

            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => HelperAsync();

                private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            namespace Sample;

            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken = default) => HelperAsync(token: cancellationToken);

                private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    // ---- Call-site propagation ------------------------------------------------------------------------------

    [Test]
    public Task CallToMethodWithUnfilledTrailingToken_AppendsTokenArgument() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    return HelperAsync(value);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    return HelperAsync(value, token: cancellationToken);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallToShorterOverload_AppendsTokenArgumentSoLongerOverloadIsChosen() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value);
                    return Task.CompletedTask;
                }

                private static void HelperAsync(int value) { }

                private static Task HelperAsync(int value, CancellationToken token) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value, token: cancellationToken);
                    return Task.CompletedTask;
                }

                private static void HelperAsync(int value) { }

                private static Task HelperAsync(int value, CancellationToken token) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task ExpressionBodiedMethod_PropagatesIntoExpressionBody() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value) => HelperAsync(value);

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default) => HelperAsync(value, token: cancellationToken);

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallAlreadyPassingToken_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    var other = CancellationToken.None;
                    return HelperAsync(value, other);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    var other = CancellationToken.None;
                    return HelperAsync(value, other);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallInsideLocalFunction_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value);
                    return Local();

                    Task Local() => HelperAsync(value);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value, token: cancellationToken);
                    return Local();

                    Task Local() => HelperAsync(value);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    // ---- Using-directive insertion edge cases ----------------------------------------------------------

    [Test]
    public Task BlockScopedNamespaceWithExistingUsings_UsingInsertedAtNamespaceLevel() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            namespace Sample
            {
                using System.Threading;
                using System.Threading.Tasks;

                public sealed class Sample
                {
                    public Task {|NE0010:Run|}() => HelperAsync();

                    private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
                }
            }
            """,
            """
            namespace Sample
            {
                using System.Threading;
                using System.Threading.Tasks;

                public sealed class Sample
                {
                    public Task Run(CancellationToken cancellationToken = default) => HelperAsync(token: cancellationToken);

                    private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;
                }
            }
            """
        );

    [Test]
    public Task NoUsingsAnywhereInFile_AddsUsingAtTop() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            public sealed class Sample
            {
                public System.Threading.Tasks.Task {|NE0010:Run|}() => HelperAsync();

                private static System.Threading.Tasks.Task HelperAsync(System.Threading.CancellationToken token = default) =>
                    System.Threading.Tasks.Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            public sealed class Sample
            {
                public System.Threading.Tasks.Task Run(CancellationToken cancellationToken = default) => HelperAsync(token: cancellationToken);

                private static System.Threading.Tasks.Task HelperAsync(System.Threading.CancellationToken token = default) =>
                    System.Threading.Tasks.Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task NewUsingSortsAfterAllExisting_AppendedAtEnd() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                public System.Threading.Tasks.Task {|NE0010:Run|}()
                {
                    _ = new List<int>();
                    return HelperAsync();
                }

                private static System.Threading.Tasks.Task HelperAsync(
                    System.Threading.CancellationToken token = default
                ) => System.Threading.Tasks.Task.CompletedTask;
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading;

            public sealed class Sample
            {
                public System.Threading.Tasks.Task Run(CancellationToken cancellationToken = default)
                {
                    _ = new List<int>();
                    return HelperAsync(token: cancellationToken);
                }

                private static System.Threading.Tasks.Task HelperAsync(
                    System.Threading.CancellationToken token = default
                ) => System.Threading.Tasks.Task.CompletedTask;
            }
            """
        );

    // ---- Call-site propagation edge cases --------------------------------------------------------------

    [Test]
    public Task SiblingOverloadWithMismatchedLeadingParameter_IsNotAppendable() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value);
                    return OtherAsync();
                }

                private static void HelperAsync(int value) { }

                private static Task HelperAsync(string value, CancellationToken token) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value);
                    return OtherAsync(token: cancellationToken);
                }

                private static void HelperAsync(int value) { }

                private static Task HelperAsync(string value, CancellationToken token) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallToMethodWithOptionalParameterAfterToken_AppendsTokenByName() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    return HelperAsync(value);
                }

                private static Task HelperAsync(int value, CancellationToken token = default, bool flag = false) =>
                    Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    return HelperAsync(value, token: cancellationToken);
                }

                private static Task HelperAsync(int value, CancellationToken token = default, bool flag = false) =>
                    Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallToMethodWithParamsArrayAfterToken_AppendsTokenByName() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    return HelperAsync(value);
                }

                private static Task HelperAsync(int value, CancellationToken token = default, params object[] extra) =>
                    Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    return HelperAsync(value, token: cancellationToken);
                }

                private static Task HelperAsync(int value, CancellationToken token = default, params object[] extra) =>
                    Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallWithExistingNamedArgument_GetsTokenAppended() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value: value);
                    return OtherAsync();
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value: value, token: cancellationToken);
                    return OtherAsync(token: cancellationToken);
                }

                private static Task HelperAsync(int value, CancellationToken token = default) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallWithPositionalArgumentFollowedByUnrelatedNamedArgument_GetsTokenAppended() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(string text)
                {
                    return HelperAsync(text, path: "Sample.cs");
                }

                private static Task HelperAsync(
                    string text,
                    string? options = null,
                    string path = "",
                    CancellationToken token = default
                ) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(string text, CancellationToken cancellationToken = default)
                {
                    return HelperAsync(text, path: "Sample.cs", token: cancellationToken);
                }

                private static Task HelperAsync(
                    string text,
                    string? options = null,
                    string path = "",
                    CancellationToken token = default
                ) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallWithRefArgument_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(ref value);
                    return OtherAsync();
                }

                private static Task HelperAsync(ref int value, CancellationToken token = default) =>
                    Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(ref value);
                    return OtherAsync(token: cancellationToken);
                }

                private static Task HelperAsync(ref int value, CancellationToken token = default) =>
                    Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallToMethodWithAmbiguousCancellationTokenParameters_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value);
                    return OtherAsync();
                }

                private static Task HelperAsync(
                    int value,
                    CancellationToken token1 = default,
                    CancellationToken token2 = default
                ) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value);
                    return OtherAsync(token: cancellationToken);
                }

                private static Task HelperAsync(
                    int value,
                    CancellationToken token1 = default,
                    CancellationToken token2 = default
                ) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallToMethodWithSiblingOverloadAddingUnrelatedParameter_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value);
                    return OtherAsync();
                }

                private static void HelperAsync(int value) { }

                private static Task HelperAsync(int value, bool flag) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value);
                    return OtherAsync(token: cancellationToken);
                }

                private static void HelperAsync(int value) { }

                private static Task HelperAsync(int value, bool flag) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallResolvingToNoMethodSymbol_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(dynamic value)
                {
                    value.HelperAsync();
                    return OtherAsync();
                }

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(dynamic value, CancellationToken cancellationToken = default)
                {
                    value.HelperAsync();
                    return OtherAsync(token: cancellationToken);
                }

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task CallSupplyingMoreArgumentsThanParameters_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value, 1, 2, 3);
                    return OtherAsync();
                }

                private static Task HelperAsync(int value, params int[] rest) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value, 1, 2, 3);
                    return OtherAsync(token: cancellationToken);
                }

                private static Task HelperAsync(int value, params int[] rest) => Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );

    [Test]
    public Task SiblingOverloadRequiringExtraParameter_IsLeftUnchanged() =>
        CSharpCodeFixVerifier<
            RequireCancellationTokenParameterAnalyzer,
            RequireCancellationTokenParameterCodeFixProvider
        >.VerifyCodeFixAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value)
                {
                    HelperAsync(value);
                    return OtherAsync();
                }

                private static void HelperAsync(int value, string label = null) { }

                private static Task HelperAsync(int value, CancellationToken token, string label) =>
                    Task.CompletedTask;

                private static Task HelperAsync(int value, string label, bool extra, CancellationToken token) =>
                    Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default)
                {
                    HelperAsync(value);
                    return OtherAsync(token: cancellationToken);
                }

                private static void HelperAsync(int value, string label = null) { }

                private static Task HelperAsync(int value, CancellationToken token, string label) =>
                    Task.CompletedTask;

                private static Task HelperAsync(int value, string label, bool extra, CancellationToken token) =>
                    Task.CompletedTask;

                private static Task OtherAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """
        );
}
