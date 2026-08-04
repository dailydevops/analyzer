namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end unit tests for the NE0003 flatten fix through a real <c>AdhocWorkspace</c> (see
/// <see cref="SingleNamespaceCodeFixRunner"/>). These mirror the integration coverage so the fix is exercised
/// by both test flags, and cover the rendering branches (leading/interior blank lines, usings, enum/delegate
/// members, trailing-newline style, folder-derived vs concatenated target).
/// </summary>
public sealed class SingleNamespacePerFileCodeFixRunnerTests
{
    // Nested file with usings, a doc comment, a leading blank line, an interior blank line, and enum/delegate
    // members — exercises the full member-rendering path. No anchor properties, so the target is the
    // concatenated nesting chain.
    private const string Rich =
        "using System;\n\nnamespace Outer\n{\n    namespace Inner\n    {\n\n"
        + "        /// <summary>A circle.</summary>\n        public sealed class Circle\n        {\n"
        + "            public int X { get; }\n\n            public int Y { get; }\n        }\n\n"
        + "        public enum Kind { A }\n\n        public delegate void Handler();\n    }\n}\n";

    [Test]
    public async Task Nested_Rich_FlattensToConcatenatedChain()
    {
        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync("Types.cs", Rich, filePath: "Types.cs")
            .ConfigureAwait(false);

        var text = result["Types.cs"];
        await Assert.That(text.Contains("namespace Outer.Inner;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("using System;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("/// <summary>A circle.</summary>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("public enum Kind { A }", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("public delegate void Handler();", StringComparison.Ordinal)).IsTrue();
        // De-indented to column 0.
        await Assert.That(text.Contains("\npublic sealed class Circle", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Nested_NoTrailingNewline_WithAnchor_UsesFolderNamespace()
    {
        const string source =
            "namespace Outer\n{\n    namespace Inner\n    {\n        public sealed class Circle { }\n    }\n}";

        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync(
                "Circle.cs",
                source,
                filePath: "/proj/Shapes/Circle.cs",
                properties: [("RootNamespace", "Geometry"), ("ProjectDir", "/proj")]
            )
            .ConfigureAwait(false);

        var text = result["Circle.cs"];
        await Assert.That(text.Contains("namespace Geometry.Shapes;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.EndsWith('\n', StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task SiblingNamespaces_OfferNoFix_LeavesFileUnchanged()
    {
        const string source =
            "namespace Alpha\n{\n    public sealed class One { }\n}\n\nnamespace Beta\n{\n    public sealed class Two { }\n}\n";

        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync("Types.cs", source, filePath: "Types.cs")
            .ConfigureAwait(false);

        var text = result["Types.cs"];
        await Assert.That(text.Contains("namespace Alpha", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("namespace Beta", StringComparison.Ordinal)).IsTrue();
    }
}
