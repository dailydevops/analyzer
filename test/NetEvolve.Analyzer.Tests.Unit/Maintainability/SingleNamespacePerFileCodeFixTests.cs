namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System.Threading.Tasks;
using TUnit.Core;

/// <summary>
/// Tests for <c>SingleNamespacePerFileCodeFixProvider</c> (NE0003): flattening a nested namespace to a single
/// file-scoped namespace. Without <c>RootNamespace</c>/<c>ProjectDir</c> the target is the literal dotted
/// concatenation of the nested chain, so the outcome is deterministic in the unit harness.
/// </summary>
public sealed class SingleNamespacePerFileCodeFixTests
{
    [Test]
    public Task Nested_Flatten_UsesConcatenatedChainFallback() =>
        SingleNamespacePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Outer
                    {
                        namespace {|NE0003:Inner|}
                        {
                            public sealed class Circle { }
                        }
                    }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Outer.Inner;

                    public sealed class Circle { }
                    """
                ),
            ]
        );
}
