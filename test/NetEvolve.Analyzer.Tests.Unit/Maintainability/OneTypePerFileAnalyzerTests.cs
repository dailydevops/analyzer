namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Maintainability;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>Unit tests for <c>OneTypePerFileAnalyzer</c> (NE0001), driven through the verifier harness.</summary>
public sealed class OneTypePerFileAnalyzerTests
{
    [Test]
    public async Task Initialize_NullContext_ThrowsArgumentNullException()
    {
        var analyzer = new OneTypePerFileAnalyzer();
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

    // ---- Compliant: single type matching the file name --------------------------------------------------

    [Test]
    public Task SingleType_FileScopedNamespace_MatchingName_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            public sealed class Circle { }
            """
        );

    [Test]
    public Task SingleType_BlockNamespace_MatchingName_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry
            {
                public sealed class Circle { }
            }
            """
        );

    [Test]
    public Task SingleType_NoNamespace_MatchingName_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync("Circle.cs", "public sealed class Circle { }");

    [Test]
    public Task NestedTypes_AreIgnored_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Outer.cs",
            """
            namespace Geometry;

            public sealed class Outer
            {
                private sealed class Inner { }

                private enum Kind { One }
            }
            """
        );

    [Test]
    public Task FileScopedType_MismatchedName_IsIgnored_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            public sealed class Circle { }

            file sealed class Helper { }
            """
        );

    [Test]
    public Task FileScopedDelegate_MismatchedName_IsIgnored_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            public sealed class Circle { }

            file delegate void Helper();
            """
        );

    [Test]
    public Task FileScopedType_Only_IsIgnored_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            file sealed class Helper { }
            """
        );

    [Test]
    public Task PartialType_MultipleParts_SameFile_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            public sealed partial class Circle { }

            public sealed partial class Circle { }
            """
        );

    [Test]
    public Task FileWithoutTopLevelType_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync("Empty.cs", "// intentionally without a top-level type");

    [Test]
    public Task PartialType_SuffixedFileName_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.Drawing.cs",
            """
            namespace Geometry;

            public sealed partial class Circle { }
            """
        );

    [Test]
    public Task PartialType_MultipleSuffixedFiles_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync([
            (
                "Circle.cs",
                """
                namespace Geometry;

                public sealed partial class Circle { }
                """
            ),
            (
                "Circle.Drawing.cs",
                """
                namespace Geometry;

                public sealed partial class Circle { }
                """
            ),
            (
                "Circle.Serialization.cs",
                """
                namespace Geometry;

                public sealed partial class Circle { }
                """
            ),
        ]);

    [Test]
    public Task PartialType_NoBaseFile_AllSuffixedFiles_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync([
            (
                "Circle.Drawing.cs",
                """
                namespace Geometry;

                public sealed partial class Circle { }
                """
            ),
            (
                "Circle.Serialization.cs",
                """
                namespace Geometry;

                public sealed partial class Circle { }
                """
            ),
        ]);

    // ---- Non-compliant: name mismatch and multiple types ------------------------------------------------

    [Test]
    public Task SingleType_NameMismatch_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Shapes.cs",
            """
            namespace Geometry;

            public sealed class {|NE0001:Circle|} { }
            """
        );

    [Test]
    public Task SingleType_NoNamespace_NameMismatch_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync("Wrong.cs", "public sealed class {|NE0001:Circle|} { }");

    [Test]
    public Task MultipleTypes_NoMatchingName_AllFlagged() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Shapes.cs",
            """
            namespace Geometry;

            public sealed class {|NE0001:Circle|} { }

            public sealed class {|NE0001:Square|} { }
            """
        );

    [Test]
    public Task MultipleTypes_OneMatchesFileName_OnlyOthersFlagged() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.cs",
            """
            namespace Geometry;

            public sealed class Circle { }

            public sealed class {|NE0001:Square|} { }
            """
        );

    [Test]
    public Task SameName_DifferentNamespaces_AreDistinctTypes_AllFlagged() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Types.cs",
            """
            namespace Models
            {
                public sealed class {|NE0001:Item|} { }
            }

            namespace Dtos
            {
                public sealed class {|NE0001:Item|} { }
            }
            """
        );

    [Test]
    public Task SameName_DifferentNamespaces_OneMatchesFileName_OnlyOtherFlagged() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Item.cs",
            """
            namespace Models
            {
                public sealed class Item { }
            }

            namespace Dtos
            {
                public sealed class {|NE0001:Item|} { }
            }
            """
        );

    [Test]
    public Task NonPartialType_SuffixedFileName_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Circle.Drawing.cs",
            """
            namespace Geometry;

            public sealed class {|NE0001:Circle|} { }
            """
        );

    [Test]
    public Task PartialType_UnrelatedSuffixedFileName_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "CircleDrawing.cs",
            """
            namespace Geometry;

            public sealed partial class {|NE0001:Circle|} { }
            """
        );

    [Test]
    public Task PartialType_NameMismatch_ReportedOnce() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Wrong.cs",
            """
            namespace Geometry;

            public sealed partial class {|NE0001:Circle|} { }

            public sealed partial class Circle { }
            """
        );

    // ---- Type kinds -------------------------------------------------------------------------------------

    [Test]
    public Task Enum_NameMismatch_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Wrong.cs",
            """
            namespace Geometry;

            public enum {|NE0001:Color|} { Red }
            """
        );

    [Test]
    public Task Delegate_NameMismatch_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Wrong.cs",
            """
            namespace Geometry;

            public delegate void {|NE0001:Handler|}();
            """
        );

    [Test]
    public Task GenericDelegate_NameMismatch_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Wrong.cs",
            """
            namespace Geometry;

            public delegate T {|NE0001:Factory|}<T>();
            """
        );

    [Test]
    public Task Record_NameMismatch_Diagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Wrong.cs",
            """
            namespace Geometry;

            public sealed record {|NE0001:Point|}(int X, int Y);
            """
        );

    // ---- Generic overloads: strict (default) ------------------------------------------------------------

    [Test]
    public Task Generic_Strict_NonGenericInBaseNamedFile_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Result.cs",
            """
            namespace Geometry;

            public readonly struct Result { }
            """
        );

    [Test]
    public Task Generic_Strict_ArityEncodedFileName_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Result{T}.cs",
            """
            namespace Geometry;

            public readonly struct Result<T> { }
            """
        );

    [Test]
    public Task Generic_Strict_TwoArgArityEncodedFileName_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Result{T1,T2}.cs",
            """
            namespace Geometry;

            public readonly struct Result<T1, T2> { }
            """
        );

    [Test]
    public Task Generic_Strict_OverloadsGroupedInBaseFile_ExtraFlagged() =>
        OneTypePerFileVerifier.VerifyAsync(
            "Result.cs",
            """
            namespace Geometry;

            public readonly struct Result { }

            public readonly struct {|NE0001:Result|}<T> { }
            """
        );

    // ---- Generic overloads: grouping enabled ------------------------------------------------------------

    [Test]
    public Task Generic_Grouped_AllOverloadsInBaseFile_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            [
                (
                    "Result.cs",
                    """
                    namespace Geometry;

                    public readonly struct Result { }

                    public readonly struct Result<T> { }

                    public readonly struct Result<T1, T2> { }
                    """
                ),
            ],
            ("NetEvolveAnalyzerGroupGenericOverloads", "true")
        );

    [Test]
    public Task Generic_Grouped_FileNameMismatch_ReportedOnce() =>
        OneTypePerFileVerifier.VerifyAsync(
            [
                (
                    "Shapes.cs",
                    """
                    namespace Geometry;

                    public readonly struct {|NE0001:Result|} { }

                    public readonly struct Result<T> { }
                    """
                ),
            ],
            ("NetEvolveAnalyzerGroupGenericOverloads", "true")
        );

    // ---- Opt-outs ---------------------------------------------------------------------------------------

    [Test]
    public Task Disabled_ViaBuildProperty_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            [
                (
                    "Shapes.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
            ],
            ("NetEvolveAnalyzerDisableFileOrganizationRules", "true")
        );

    [Test]
    public Task Disabled_ForSingleFilePublish_NoDiagnostic() =>
        OneTypePerFileVerifier.VerifyAsync(
            [
                (
                    "Shapes.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
            ],
            ("PublishSingleFile", "true")
        );
}
