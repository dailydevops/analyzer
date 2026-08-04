namespace NetEvolve.Analyzer.Tests.Unit.Helpers;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Unit tests for the shared <see cref="FolderNamespace"/> helper — the folder-to-namespace mapping that both
/// NE0002 and the NE0003 flatten fix rely on. Exercised directly so every branch (missing anchors, root-level
/// files, files outside the project, invalid folder segments) is covered without the analyzer harness.
/// </summary>
public sealed class FolderNamespaceTests
{
    [Test]
    public async Task TryResolve_EmptyFilePath_ReturnsFalse()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", "Geometry"), ("ProjectDir", "/proj")),
            "",
            out _
        );

        await Assert.That(resolved).IsFalse();
    }

    [Test]
    public async Task TryResolve_MissingRootNamespaceOrProjectDir_ReturnsFalse()
    {
        var resolved = FolderNamespace.TryResolve(Options(), "/proj/Shapes/Circle.cs", out _);

        await Assert.That(resolved).IsFalse();
    }

    [Test]
    public async Task TryResolve_NoDirectoryComponent_MapsToRootNamespace()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", "Geometry"), ("ProjectDir", "/proj")),
            "Circle.cs",
            out var expected
        );

        await Assert.That(resolved).IsTrue();
        await Assert.That(expected).IsEqualTo("Geometry");
    }

    [Test]
    public async Task TryResolve_FileInProjectRoot_MapsToRootNamespace()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", "Geometry"), ("ProjectDir", "/proj")),
            "/proj/Circle.cs",
            out var expected
        );

        await Assert.That(resolved).IsTrue();
        await Assert.That(expected).IsEqualTo("Geometry");
    }

    [Test]
    public async Task TryResolve_SubFolders_JoinsSegments()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", "Geometry"), ("ProjectDir", "/proj")),
            "/proj/Shapes/Primitives/Circle.cs",
            out var expected
        );

        await Assert.That(resolved).IsTrue();
        await Assert.That(expected).IsEqualTo("Geometry.Shapes.Primitives");
    }

    [Test]
    public async Task TryResolve_EmptyRootNamespace_SubFolders_JoinsSegmentsAlone()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", ""), ("ProjectDir", "/proj")),
            "/proj/Shapes/Primitives/Circle.cs",
            out var expected
        );

        await Assert.That(resolved).IsTrue();
        await Assert.That(expected).IsEqualTo("Shapes.Primitives");
    }

    [Test]
    public async Task TryResolve_MissingRootNamespace_SubFolders_JoinsSegmentsAlone()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("ProjectDir", "/proj")),
            "/proj/Shapes/Circle.cs",
            out var expected
        );

        await Assert.That(resolved).IsTrue();
        await Assert.That(expected).IsEqualTo("Shapes");
    }

    [Test]
    public async Task TryResolve_EmptyRootNamespace_ProjectRoot_ReturnsFalse()
    {
        // No folders to compose from and no RootNamespace anchor: nothing reliable to map to, so stay silent.
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", ""), ("ProjectDir", "/proj")),
            "/proj/Circle.cs",
            out _
        );

        await Assert.That(resolved).IsFalse();
    }

    [Test]
    public async Task TryResolve_OutsideProjectDir_ReturnsFalse()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", "Geometry"), ("ProjectDir", "/proj")),
            "/other/Circle.cs",
            out _
        );

        await Assert.That(resolved).IsFalse();
    }

    [Test]
    public async Task TryResolve_InvalidIdentifierSegment_ReturnsFalse()
    {
        var resolved = FolderNamespace.TryResolve(
            Options(("RootNamespace", "Geometry"), ("ProjectDir", "/proj")),
            "/proj/my-folder/Circle.cs",
            out _
        );

        await Assert.That(resolved).IsFalse();
    }

    private static FakeOptions Options(params (string Key, string Value)[] properties)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in properties)
        {
            builder["build_property." + key] = value;
        }

        return new FakeOptions(builder.ToImmutable());
    }

    private sealed class FakeOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _values;

        public FakeOptions(ImmutableDictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            _values.TryGetValue(key, out value);
    }
}
