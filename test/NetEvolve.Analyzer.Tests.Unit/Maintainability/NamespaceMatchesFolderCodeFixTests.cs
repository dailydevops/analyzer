namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System.Threading.Tasks;
using TUnit.Core;

/// <summary>
/// Tests for <c>NamespaceMatchesFolderCodeFixProvider</c> (NE0002). The rule fires only when a file's folder
/// path resolves against <c>RootNamespace</c>/<c>ProjectDir</c>; the unit verifier's project-directory
/// convention is opaque, so the substantive fix coverage lives in the deterministic integration runner. The
/// case kept here confirms that without an anchor mapping nothing is flagged and no fix is applied.
/// </summary>
public sealed class NamespaceMatchesFolderCodeFixTests
{
    [Test]
    public Task NoBuildProperties_NoDiagnostic_NoFix() =>
        NamespaceMatchesFolderCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Whatever;

                    public sealed class Circle { }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Whatever;

                    public sealed class Circle { }
                    """
                ),
            ]
        );
}
