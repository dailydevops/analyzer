namespace NetEvolve.Analyzer.Tests.Integration.Maintainability;

using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for the NE0001 code fix through a real <c>AdhocWorkspace</c> (see <see cref="CodeFixRunner"/>),
/// complementing the verifier-based unit tests and exercising the fix's actual apply pipeline.
/// </summary>
public sealed class OneTypePerFileCodeFixTests
{
    [Test]
    public async Task Rename_SingleType_RenamesDocument()
    {
        var result = await CodeFixRunner
            .ApplyAsync([("Shapes.cs", "namespace Geometry;\n\npublic sealed class Circle { }\n")])
            .ConfigureAwait(false);

        await Assert.That(result.ContainsKey("Circle.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Shapes.cs")).IsFalse();
    }

    [Test]
    public async Task Move_FileScoped_AddsNewFileKeepsPrimary()
    {
        const string source =
            "namespace Geometry;\n\npublic sealed class Circle { }\n\npublic sealed class Square { }\n";

        var result = await CodeFixRunner.ApplyAsync([("Circle.cs", source)]).ConfigureAwait(false);

        await Assert.That(result.ContainsKey("Circle.cs")).IsTrue();
        await Assert
            .That(
                result.TryGetValue("Square.cs", out var square)
                    && square.Contains("class Square", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Move_NestedBlockNamespace_UsesFullDottedName()
    {
        const string source =
            "namespace Outer\n{\n    namespace Inner\n    {\n        public sealed class Circle { }\n\n        public sealed class Square { }\n    }\n}\n";

        var result = await CodeFixRunner.ApplyAsync([("Circle.cs", source)]).ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Square.cs", out var square)
                    && square.Contains("namespace Outer.Inner;", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Move_NoNamespace_AddsNewFile()
    {
        const string source = "public sealed class Circle { }\n\npublic sealed class Square { }\n";

        var result = await CodeFixRunner.ApplyAsync([("Circle.cs", source)]).ConfigureAwait(false);

        await Assert.That(result.ContainsKey("Square.cs")).IsTrue();
    }

    [Test]
    public async Task Move_CarriesUsingsAndDocComment()
    {
        const string source =
            "using System;\n\nnamespace Geometry;\n\npublic sealed class Circle { }\n\n/// <summary>A clock.</summary>\npublic sealed class Clock\n{\n    public DateTime Now { get; }\n}\n";

        var result = await CodeFixRunner.ApplyAsync([("Circle.cs", source)]).ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Clock.cs", out var clock)
                    && clock.Contains("using System;", StringComparison.Ordinal)
                    && clock.Contains("<summary>A clock.</summary>", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Move_GroupedGenericOverloads_TogetherToBaseFile()
    {
        const string source =
            "namespace Geometry;\n\npublic sealed class Anchor { }\n\npublic readonly struct Result { }\n\npublic readonly struct Result<T> { }\n";

        var result = await CodeFixRunner
            .ApplyAsync([("Anchor.cs", source)], properties: [("NetEvolveAnalyzerGroupGenericOverloads", "true")])
            .ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Result.cs", out var file)
                    && file.Contains("struct Result<T>", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Move_Enum_KindHandled()
    {
        const string source = "namespace Geometry;\n\npublic sealed class Circle { }\n\npublic enum Color { Red }\n";

        var result = await CodeFixRunner.ApplyAsync([("Circle.cs", source)]).ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Color.cs", out var color) && color.Contains("enum Color", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Move_TargetEqualsCurrentFile_MakesNoChange()
    {
        const string source =
            "namespace Models\n{\n    public sealed class Item { }\n}\n\nnamespace Dtos\n{\n    public sealed class Item { }\n}\n";

        var result = await CodeFixRunner.ApplyAsync([("Item.cs", source)]).ConfigureAwait(false);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.ContainsKey("Item.cs")).IsTrue();
    }
}
