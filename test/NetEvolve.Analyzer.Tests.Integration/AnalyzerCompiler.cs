namespace NetEvolve.Analyzer.Tests.Integration;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Compiles a piece of source into a real <see cref="Compilation"/> against the running framework and, when
/// given analyzers, drives them through <see cref="CompilationWithAnalyzers"/> — the same pipeline the
/// compiler uses. This exercises rules end-to-end rather than through the unit-test verifier harness.
/// </summary>
internal static class AnalyzerCompiler
{
    private static readonly ImmutableArray<MetadataReference> _references = ResolveFrameworkReferences();

    /// <summary>Creates a compilation for <paramref name="source"/> against the running framework's assemblies.</summary>
    public static CSharpCompilation CreateCompilation(
        string source,
        string? path = null,
        CancellationToken cancellationToken = default
    ) =>
        CSharpCompilation.Create(
            assemblyName: "NetEvolve.Analyzer.Integration.Sample",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source, path: path ?? string.Empty, cancellationToken: cancellationToken),
            ],
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    /// <summary>Returns the plain compiler diagnostics (syntax and semantic) for <paramref name="source"/>.</summary>
    public static ImmutableArray<Diagnostic> GetCompilerDiagnostics(
        string source,
        CancellationToken cancellationToken = default
    ) => CreateCompilation(source, cancellationToken: cancellationToken).GetDiagnostics(cancellationToken);

    /// <summary>
    /// Runs <paramref name="analyzer"/> over a compilation of <paramref name="source"/>, giving the tree the
    /// supplied <paramref name="path"/> and exposing <paramref name="properties"/> as MSBuild build properties.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        string? path = null,
        (string Key, string Value)[]? properties = null,
        CancellationToken cancellationToken = default
    )
    {
        var options = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new BuildPropertyOptionsProvider(properties)
        );

        // S8949: the cancellation-token WithAnalyzers overload is obsolete; cancellation is honored by the
        // GetAnalyzerDiagnosticsAsync call below, which is the only place work actually happens.
#pragma warning disable S8949
        var withAnalyzers = CreateCompilation(source, path, cancellationToken)
            .WithAnalyzers(ImmutableArray.Create(analyzer), options);
#pragma warning restore S8949

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ImmutableArray<MetadataReference> ResolveFrameworkReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return
        [
            .. trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length != 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    }

    /// <summary>Surfaces the given <c>build_property.*</c> pairs through <see cref="AnalyzerConfigOptions"/>.</summary>
    private sealed class BuildPropertyOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly BuildPropertyOptions _options;

        public BuildPropertyOptionsProvider((string Key, string Value)[]? properties)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in properties ?? [])
            {
                builder["build_property." + key] = value;
            }

            _options = new BuildPropertyOptions(builder.ToImmutable());
        }

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

        private sealed class BuildPropertyOptions : AnalyzerConfigOptions
        {
            private readonly ImmutableDictionary<string, string> _values;

            public BuildPropertyOptions(ImmutableDictionary<string, string> values) => _values = values;

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
                _values.TryGetValue(key, out value);
        }
    }
}
