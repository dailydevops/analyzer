namespace NetEvolve.Analyzer.Tests.Integration.Maintainability;

using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for the NE0002 code fix through a real <c>AdhocWorkspace</c> (see <see cref="NamespaceCodeFixRunner"/>),
/// where the file path and <c>RootNamespace</c>/<c>ProjectDir</c> are deterministic so the folder-derived
/// namespace resolves and the fix's actual apply pipeline is exercised.
/// </summary>
public sealed class NamespaceMatchesFolderCodeFixTests
{
    private static readonly (string Key, string Value)[] Anchor =
    [
        ("RootNamespace", "Geometry"),
        ("ProjectDir", "/proj"),
    ];

    [Test]
    public async Task Mismatch_RewritesNamespaceToFolderDerived()
    {
        const string source = "namespace Geometry;\n\npublic sealed class Circle { }\n";

        var result = await NamespaceCodeFixRunner
            .ApplyAsync([("/proj/Shapes/Circle.cs", source)], properties: Anchor)
            .ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("/proj/Shapes/Circle.cs", out var fixedText)
                    && fixedText.Contains("namespace Geometry.Shapes;", StringComparison.Ordinal)
            )
            .IsTrue();
    }

    [Test]
    public async Task Mismatch_NestedFolder_RewritesToFullDottedNamespace()
    {
        const string source = "namespace Geometry.Shapes;\n\npublic sealed class Circle { }\n";

        var result = await NamespaceCodeFixRunner
            .ApplyAsync([("/proj/Shapes/Primitives/Circle.cs", source)], properties: Anchor)
            .ConfigureAwait(false);

        await Assert
            .That(
                result.TryGetValue("/proj/Shapes/Primitives/Circle.cs", out var fixedText)
                    && fixedText.Contains("namespace Geometry.Shapes.Primitives;", StringComparison.Ordinal)
            )
            .IsTrue();
    }
}
