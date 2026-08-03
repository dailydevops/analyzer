namespace NetEvolve.Analyzer.Tests.Integration.Maintainability;

using System;
using System.Threading.Tasks;
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
}
