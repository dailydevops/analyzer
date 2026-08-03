namespace NetEvolve.Analyzer.Tests.Integration.Maintainability;

using System;
using System.Threading.Tasks;
using NetEvolve.Analyzer.Maintainability;
using NetEvolve.Analyzer.Providers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for the NE0003 code fix through a real <c>AdhocWorkspace</c> (see
/// <see cref="SingleNamespaceCodeFixRunner"/>): flattening a nested namespace either to the folder-derived
/// namespace or, when no anchor is available, to the concatenated nested chain.
/// </summary>
public sealed class SingleNamespacePerFileCodeFixTests
{
    private const string Nested = """
        namespace Outer
        {
            namespace Inner
            {
                public sealed class Circle { }
            }
        }
        """;

    [Test]
    public async Task Nested_NoProperties_UsesConcatenatedChainFallback()
    {
        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync("Circle.cs", Nested, filePath: "Circle.cs")
            .ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Circle.cs", out var text)
                    && text.Contains("namespace Outer.Inner;", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Nested_WithRootAndProjectDir_UsesFolderDerivedNamespace()
    {
        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync(
                "Circle.cs",
                Nested,
                filePath: "/proj/Shapes/Circle.cs",
                properties: [("RootNamespace", "Geometry"), ("ProjectDir", "/proj")]
            )
            .ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Circle.cs", out var text)
                    && text.Contains("namespace Geometry.Shapes;", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Nested_WithUsings_CarriesUsingsIntoFlattenedFile()
    {
        const string source = """
            using System;

            namespace Outer
            {
                namespace Inner
                {
                    public sealed class Circle
                    {
                        public DateTime Now { get; }
                    }
                }
            }
            """;

        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync("Circle.cs", source, filePath: "Circle.cs")
            .ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("Circle.cs", out var text)
                    && text.Contains("using System;", StringComparison.Ordinal)
                    && text.Contains("namespace Outer.Inner;", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task SiblingNamespaces_OfferNoFix_LeavesFileUnchanged()
    {
        const string source = """
            namespace Alpha
            {
                public sealed class One { }
            }

            namespace Beta
            {
                public sealed class Two { }
            }
            """;

        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync("Types.cs", source, filePath: "Types.cs")
            .ConfigureAwait(false);

        // The sibling case is left to NE0001's move fix, so NE0003 offers no action and the file is untouched.
        await Assert
            .That(
                result.TryGetValue("Types.cs", out var text)
                    && text.Contains("namespace Alpha", StringComparison.Ordinal)
                    && text.Contains("namespace Beta", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Nested_Rich_RendersEveryMemberKindAtColumnZero()
    {
        // Usings, a doc comment, a leading blank line, an interior blank line, plus enum and delegate members
        // exercise the full member-rendering path. No anchor properties, so the target is the concatenated chain.
        const string source =
            "using System;\n\nnamespace Outer\n{\n    namespace Inner\n    {\n\n"
            + "        /// <summary>A circle.</summary>\n        public sealed class Circle\n        {\n"
            + "            public int X { get; }\n\n            public int Y { get; }\n        }\n\n"
            + "        public enum Kind { A }\n\n        public delegate void Handler();\n    }\n}\n";

        var result = await SingleNamespaceCodeFixRunner
            .ApplyAsync("Types.cs", source, filePath: "Types.cs")
            .ConfigureAwait(false);

        var text = result["Types.cs"];
        await Assert.That(text.Contains("namespace Outer.Inner;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("using System;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("public enum Kind { A }", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("public delegate void Handler();", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("\npublic sealed class Circle", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Nested_NoTrailingNewline_PreservesStyleAndUsesFolderNamespace()
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
        await Assert.That(text.EndsWith('\n')).IsFalse();
    }

    [Test]
    public async Task GetFixAllProvider_ReturnsCustomProvider()
    {
        var provider = new SingleNamespacePerFileCodeFixProvider().GetFixAllProvider();

        await Assert.That(provider).IsSameReferenceAs(new SingleNamespacePerFileCodeFixProvider().GetFixAllProvider());
        await Assert.That(provider!.GetType()).IsEqualTo(typeof(SequentialFixAllProvider));
    }
}
