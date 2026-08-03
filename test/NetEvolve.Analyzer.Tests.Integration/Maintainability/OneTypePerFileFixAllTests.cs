namespace NetEvolve.Analyzer.Tests.Integration.Maintainability;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using NetEvolve.Analyzer.Maintainability;
using NetEvolve.Analyzer.Providers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for the NE0001 fix-all (see <see cref="FixAllRunner"/>): the custom provider must resolve
/// every occurrence in scope by applying rename/move fixes sequentially and re-resolving between steps,
/// including the move-then-rename flip and skipping the unfixable collision case.
/// </summary>
public sealed class OneTypePerFileFixAllTests
{
    [Test]
    public async Task Document_MultiTypeFile_MovesAndRenamesEveryType()
    {
        const string source = """
            namespace Geometry;

            public sealed class Circle { }

            public sealed class Square { }

            public sealed class Triangle { }
            """;

        var result = await FixAllRunner
            .FixAllAsync([("Shapes.cs", source)], FixAllScope.Document)
            .ConfigureAwait(false);

        await Assert.That(result.ContainsKey("Shapes.cs")).IsFalse();
        await Assert
            .That(
                result.TryGetValue("Circle.cs", out var circle)
                    && circle.Contains("class Circle", StringComparison.Ordinal)
            )
            .IsTrue();
        await Assert
            .That(
                result.TryGetValue("Square.cs", out var square)
                    && square.Contains("class Square", StringComparison.Ordinal)
            )
            .IsTrue();
        await Assert
            .That(
                result.TryGetValue("Triangle.cs", out var triangle)
                    && triangle.Contains("class Triangle", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Solution_TwoTypeFileNamedAfterNeither_ConvergesToOwnFiles()
    {
        const string source = """
            namespace Geometry;

            public sealed class Circle { }

            public sealed class Square { }
            """;

        var result = await FixAllRunner
            .FixAllAsync([("Shapes.cs", source)], FixAllScope.Solution)
            .ConfigureAwait(false);

        await Assert.That(result.ContainsKey("Shapes.cs")).IsFalse();
        await Assert.That(result.ContainsKey("Circle.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Square.cs")).IsTrue();
    }

    [Test]
    public async Task Project_MultipleViolatingFiles_AllResolved()
    {
        const string alpha = """
            namespace Sample;

            public sealed class Alpha { }
            """;

        const string others = """
            namespace Sample;

            public sealed class Beta { }

            public sealed class Gamma { }
            """;

        var result = await FixAllRunner
            .FixAllAsync([("A.cs", alpha), ("B.cs", others)], FixAllScope.Project)
            .ConfigureAwait(false);

        await Assert.That(result.ContainsKey("A.cs")).IsFalse();
        await Assert.That(result.ContainsKey("B.cs")).IsFalse();
        await Assert.That(result.ContainsKey("Alpha.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Beta.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Gamma.cs")).IsTrue();
    }

    [Test]
    public async Task Project_CollisionCase_CompletesAndLeavesFileIntact()
    {
        const string source = """
            namespace Models
            {
                public sealed class Item { }
            }

            namespace Dtos
            {
                public sealed class Item { }
            }
            """;

        var result = await FixAllRunner.FixAllAsync([("Item.cs", source)], FixAllScope.Project).ConfigureAwait(false);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert
            .That(
                result.TryGetValue("Item.cs", out var item)
                    && item.Contains("namespace Models", StringComparison.Ordinal)
                    && item.Contains("namespace Dtos", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Document_NoViolations_ReturnsUnchanged()
    {
        const string source = """
            namespace Geometry;

            public sealed class Circle { }
            """;

        var result = await FixAllRunner
            .FixAllAsync([("Circle.cs", source)], FixAllScope.Document)
            .ConfigureAwait(false);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.ContainsKey("Circle.cs")).IsTrue();
    }

    [Test]
    public async Task GetFixAsync_NullContext_ThrowsArgumentNullException()
    {
        ArgumentNullException? caught = null;

        try
        {
            _ = await new OneTypePerFileCodeFixProvider().GetFixAllProvider()!.GetFixAsync(null!).ConfigureAwait(false);
        }
        catch (ArgumentNullException exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task Solution_NoViolations_ReturnsUnchanged()
    {
        const string source = """
            namespace Geometry;

            public sealed class Circle { }
            """;

        var result = await FixAllRunner
            .FixAllAsync([("Circle.cs", source)], FixAllScope.Solution)
            .ConfigureAwait(false);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.ContainsKey("Circle.cs")).IsTrue();
    }

    [Test]
    public async Task GetSupportedFixAllScopes_AreDocumentProjectSolution()
    {
        var scopes = new OneTypePerFileCodeFixProvider().GetFixAllProvider()!.GetSupportedFixAllScopes().ToList();

        await Assert.That(scopes).Contains(FixAllScope.Document);
        await Assert.That(scopes).Contains(FixAllScope.Project);
        await Assert.That(scopes).Contains(FixAllScope.Solution);
    }

    [Test]
    public async Task GetFixAllProvider_ReturnsCustomProvider()
    {
        var provider = new OneTypePerFileCodeFixProvider().GetFixAllProvider();

        await Assert.That(provider).IsSameReferenceAs(new OneTypePerFileCodeFixProvider().GetFixAllProvider());
        await Assert.That(provider!.GetType()).IsEqualTo(typeof(SequentialFixAllProvider));
    }
}
