namespace NetEvolve.Analyzer.Tests.Integration;

using System;
using System.Collections.Immutable;
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
    public static CSharpCompilation CreateCompilation(string source, CancellationToken cancellationToken = default) =>
        CSharpCompilation.Create(
            assemblyName: "NetEvolve.Analyzer.Integration.Sample",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    /// <summary>Returns the plain compiler diagnostics (syntax and semantic) for <paramref name="source"/>.</summary>
    public static ImmutableArray<Diagnostic> GetCompilerDiagnostics(
        string source,
        CancellationToken cancellationToken = default
    ) => CreateCompilation(source, cancellationToken).GetDiagnostics(cancellationToken);

    /// <summary>Runs <paramref name="analyzer"/> over a compilation of <paramref name="source"/>.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        CancellationToken cancellationToken = default
    )
    {
        var withAnalyzers = CreateCompilation(source, cancellationToken).WithAnalyzers(ImmutableArray.Create(analyzer));

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
}
