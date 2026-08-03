namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Maintainability;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <c>NamespaceMatchesFolderAnalyzer</c> (NE0002), driven through the verifier harness.</summary>
public sealed class NamespaceMatchesFolderAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new NamespaceMatchesFolderAnalyzer();
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

    // ---- No mapping available: without RootNamespace/ProjectDir the analyzer stays silent -----------------

    [Test]
    public Task NoBuildProperties_NoDiagnostic() =>
        NamespaceMatchesFolderVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Whatever;

            public sealed class Circle { }
            """
        );

    [Test]
    public Task RootNamespaceWithoutProjectDir_NoDiagnostic() =>
        NamespaceMatchesFolderVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Whatever;

            public sealed class Circle { }
            """,
            ("RootNamespace", "Geometry")
        );

    [Test]
    public Task GlobalNamespaceFile_OutOfScope_NoDiagnostic() =>
        NamespaceMatchesFolderVerifier.VerifyAsync("Circle.cs", "public sealed class Circle { }");

    // ---- Opt-outs ---------------------------------------------------------------------------------------

    [Test]
    public Task Disabled_ViaBuildProperty_NoDiagnostic() =>
        NamespaceMatchesFolderVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Whatever;

                    public sealed class Circle { }
                    """
                ),
            ],
            ("RootNamespace", "Geometry"),
            ("ProjectDir", "/proj"),
            ("NetEvolveAnalyzerDisableFileOrganizationRules", "true")
        );

    [Test]
    public Task Disabled_ForSingleFilePublish_NoDiagnostic() =>
        NamespaceMatchesFolderVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Whatever;

                    public sealed class Circle { }
                    """
                ),
            ],
            ("RootNamespace", "Geometry"),
            ("ProjectDir", "/proj"),
            ("PublishSingleFile", "true")
        );
}
