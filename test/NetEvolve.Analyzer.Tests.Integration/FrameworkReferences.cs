namespace NetEvolve.Analyzer.Tests.Integration;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

/// <summary>
/// Resolves the current runtime's framework assemblies as compilation references, shared by every runner that
/// builds an ad-hoc <see cref="Compilation"/>.
/// </summary>
internal static class FrameworkReferences
{
    public static readonly ImmutableArray<MetadataReference> All = Resolve();

    private static ImmutableArray<MetadataReference> Resolve()
    {
        // .NET Framework (net472/net48/net481) doesn't populate TRUSTED_PLATFORM_ASSEMBLIES; fall back to
        // the assemblies already loaded into this AppDomain, which cover the BCL surface test sources need.
        var trustedAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Join(
                Path.PathSeparator.ToString(),
                AppDomain
                    .CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Select(assembly => assembly.Location)
            );

        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => path.Length != 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}
