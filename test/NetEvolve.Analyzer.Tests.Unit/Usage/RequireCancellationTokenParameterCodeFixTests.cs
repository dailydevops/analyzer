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
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}(int value) => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(int value, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
                public Task {|NE0010:Run|}() => Task.CompletedTask;
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken = default) => Task.CompletedTask;
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

            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task {|NE0010:Run|}() => Task.CompletedTask;
            }
            """,
            """
            namespace Sample;

            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Sample
            {
                public Task Run(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
            """
        );
}
