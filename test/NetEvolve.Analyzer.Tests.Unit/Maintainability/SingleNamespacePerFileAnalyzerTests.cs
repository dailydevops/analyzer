namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Maintainability;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <c>SingleNamespacePerFileAnalyzer</c> (NE0003), driven through the verifier harness.</summary>
public sealed class SingleNamespacePerFileAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new SingleNamespacePerFileAnalyzer();
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

    // ---- Compliant: exactly one namespace ---------------------------------------------------------------

    [Test]
    public Task SingleFileScopedNamespace_NoDiagnostic() =>
        SingleNamespacePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            public sealed class Circle { }
            """
        );

    [Test]
    public Task SingleBlockNamespace_NoDiagnostic() =>
        SingleNamespacePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry
            {
                public sealed class Circle { }
            }
            """
        );

    // ---- Non-compliant: more than one namespace ---------------------------------------------------------

    [Test]
    public Task TwoSiblingNamespaces_SecondFlagged() =>
        SingleNamespacePerFileVerifier.VerifyAsync(
            "Types.cs",
            """
            namespace First
            {
                public sealed class One { }
            }

            namespace {|NE0003:Second|}
            {
                public sealed class Two { }
            }
            """
        );

    [Test]
    public Task NestedNamespaces_InnerFlagged() =>
        SingleNamespacePerFileVerifier.VerifyAsync(
            "Types.cs",
            """
            namespace A
            {
                namespace {|NE0003:B|}
                {
                    public sealed class Circle { }
                }
            }
            """
        );

    // ---- Opt-outs ---------------------------------------------------------------------------------------

    [Test]
    public Task Disabled_ViaBuildProperty_NoDiagnostic() =>
        SingleNamespacePerFileVerifier.VerifyAsync(
            [
                (
                    "Types.cs",
                    """
                    namespace First { }

                    namespace Second { }
                    """
                ),
            ],
            ("NetEvolveAnalyzerDisableFileOrganizationRules", "true")
        );

    [Test]
    public Task Disabled_ForSingleFilePublish_NoDiagnostic() =>
        SingleNamespacePerFileVerifier.VerifyAsync(
            [
                (
                    "Types.cs",
                    """
                    namespace First { }

                    namespace Second { }
                    """
                ),
            ],
            ("PublishSingleFile", "true")
        );
}
