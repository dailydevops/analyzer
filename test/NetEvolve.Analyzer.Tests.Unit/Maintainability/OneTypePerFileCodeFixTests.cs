namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System.Threading.Tasks;
using TUnit.Core;

/// <summary>Tests for <c>OneTypePerFileCodeFixProvider</c> (NE0001): rename-file and move-type-to-file actions.</summary>
public sealed class OneTypePerFileCodeFixTests
{
    // ---- Rename file (single type) ----------------------------------------------------------------------

    [Test]
    public Task SingleType_NameMismatch_RenamesFile() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Shapes.cs",
                    """
                    namespace Geometry;

                    public sealed class {|NE0001:Circle|} { }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
            ]
        );

    [Test]
    public Task GenericStrict_NameMismatch_RenamesToArityEncodedFile() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Wrong.cs",
                    """
                    namespace Geometry;

                    public readonly struct {|NE0001:Result|}<T> { }
                    """
                ),
            ],
            [
                (
                    "Result{T}.cs",
                    """
                    namespace Geometry;

                    public readonly struct Result<T> { }
                    """
                ),
            ]
        );

    [Test]
    public Task PartialType_NameMismatch_RenamesFile() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Wrong.cs",
                    """
                    namespace Geometry;

                    public sealed partial class {|NE0001:Circle|} { }

                    public sealed partial class Circle { }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed partial class Circle { }

                    public sealed partial class Circle { }
                    """
                ),
            ]
        );

    // ---- Move type to its own file (multiple types) -----------------------------------------------------

    [Test]
    public Task MultipleTypes_PrimaryStays_MovesFlaggedType() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }

                    public sealed class {|NE0001:Square|} { }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
                (
                    "Square.cs",
                    """
                    namespace Geometry;

                    public sealed class Square { }
                    """
                ),
            ]
        );

    [Test]
    public Task MultipleTypes_NoPrimary_MovesThenRenames() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Shapes.cs",
                    """
                    namespace Geometry;

                    public sealed class {|NE0001:Circle|} { }

                    public sealed class {|NE0001:Square|} { }
                    """
                ),
            ],
            [
                (
                    "Square.cs",
                    """
                    namespace Geometry;

                    public sealed class Square { }
                    """
                ),
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_CarriesUsingDirectives() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    using System;

                    namespace Geometry;

                    public sealed class Circle { }

                    public sealed class {|NE0001:Clock|}
                    {
                        public DateTime Now { get; }
                    }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    using System;

                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
                (
                    "Clock.cs",
                    """
                    using System;

                    namespace Geometry;

                    public sealed class Clock
                    {
                        public DateTime Now { get; }
                    }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_FromBlockNamespace_EmitsFileScopedNewFile() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry
                    {
                        public sealed class Circle { }

                        public sealed class {|NE0001:Square|} { }
                    }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry
                    {
                        public sealed class Circle { }
                    }
                    """
                ),
                (
                    "Square.cs",
                    """
                    namespace Geometry;

                    public sealed class Square { }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_BlockNamespace_PreservesInnerBlankLines() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry
                    {
                        public sealed class Circle { }

                        public sealed class {|NE0001:Point|}
                        {
                            public int X { get; }

                            public int Y { get; }
                        }
                    }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry
                    {
                        public sealed class Circle { }
                    }
                    """
                ),
                (
                    "Point.cs",
                    """
                    namespace Geometry;

                    public sealed class Point
                    {
                        public int X { get; }

                        public int Y { get; }
                    }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_PreservesFinalNewline() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    "namespace Geometry;\n\npublic sealed class Circle { }\n\npublic sealed class {|NE0001:Square|} { }\n"
                ),
            ],
            [
                ("Circle.cs", "namespace Geometry;\n\npublic sealed class Circle { }\n"),
                ("Square.cs", "namespace Geometry;\n\npublic sealed class Square { }\n"),
            ]
        );

    [Test]
    public Task Move_NoNamespace() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [("Circle.cs", "public sealed class Circle { }\n\npublic sealed class {|NE0001:Square|} { }")],
            [("Circle.cs", "public sealed class Circle { }"), ("Square.cs", "public sealed class Square { }")]
        );

    [Test]
    public Task Move_Enum_KindHandled() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }

                    public enum {|NE0001:Color|} { Red }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
                (
                    "Color.cs",
                    """
                    namespace Geometry;

                    public enum Color { Red }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_Delegate_KindHandled() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }

                    public delegate void {|NE0001:Handler|}();
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
                (
                    "Handler.cs",
                    """
                    namespace Geometry;

                    public delegate void Handler();
                    """
                ),
            ]
        );

    [Test]
    public Task Move_GenericOverloadsGrouped_TogetherToBaseFile() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Anchor.cs",
                    """
                    namespace Geometry;

                    public sealed class Anchor { }

                    public readonly struct {|NE0001:Result|} { }

                    public readonly struct Result<T> { }
                    """
                ),
            ],
            [
                (
                    "Anchor.cs",
                    """
                    namespace Geometry;

                    public sealed class Anchor { }
                    """
                ),
                (
                    "Result.cs",
                    """
                    namespace Geometry;

                    public readonly struct Result { }

                    public readonly struct Result<T> { }
                    """
                ),
            ],
            ("NetEvolveAnalyzerGroupGenericOverloads", "true")
        );

    [Test]
    public Task Move_NestedBlockNamespace_KeepsFullDottedNamespace() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Outer
                    {
                        namespace Inner
                        {
                            public sealed class Circle { }

                            public sealed class {|NE0001:Square|} { }
                        }
                    }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Outer
                    {
                        namespace Inner
                        {
                            public sealed class Circle { }
                        }
                    }
                    """
                ),
                (
                    "Square.cs",
                    """
                    namespace Outer.Inner;

                    public sealed class Square { }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_CarriesLeadingDocComment() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }

                    /// <summary>A clock.</summary>
                    public sealed class {|NE0001:Clock|} { }
                    """
                ),
            ],
            [
                (
                    "Circle.cs",
                    """
                    namespace Geometry;

                    public sealed class Circle { }
                    """
                ),
                (
                    "Clock.cs",
                    """
                    namespace Geometry;

                    /// <summary>A clock.</summary>
                    public sealed class Clock { }
                    """
                ),
            ]
        );

    [Test]
    public Task Move_TargetEqualsCurrentFile_NotOffered() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
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
                ),
            ],
            [
                (
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
                ),
            ]
        );

    [Test]
    public Task GenericStrict_TwoTypeArguments_RenamesToArityEncodedFile() =>
        OneTypePerFileCodeFixVerifier.VerifyAsync(
            [
                (
                    "Wrong.cs",
                    """
                    namespace Geometry;

                    public readonly struct {|NE0001:Result|}<T1, T2> { }
                    """
                ),
            ],
            [
                (
                    "Result{T1,T2}.cs",
                    """
                    namespace Geometry;

                    public readonly struct Result<T1, T2> { }
                    """
                ),
            ]
        );
}
