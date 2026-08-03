namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

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
/// End-to-end unit tests for the NE0001 fix-all through a real <c>AdhocWorkspace</c> (see
/// <see cref="FixAllRunner"/>). These mirror the integration coverage so the custom provider is exercised by
/// both test flags, and cover the scope enumeration, the null-argument guard, and the no-op paths.
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
        await Assert.That(result.ContainsKey("Circle.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Square.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Triangle.cs")).IsTrue();
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

        await Assert.That(result.ContainsKey("Alpha.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Beta.cs")).IsTrue();
        await Assert.That(result.ContainsKey("Gamma.cs")).IsTrue();
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
        await Assert.That(result.ContainsKey("Item.cs")).IsTrue();
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
}
