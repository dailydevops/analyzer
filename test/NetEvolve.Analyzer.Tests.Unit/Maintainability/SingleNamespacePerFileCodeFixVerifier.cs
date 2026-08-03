namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NetEvolve.Analyzer.Maintainability;

/// <summary>
/// Applies <see cref="SingleNamespacePerFileCodeFixProvider"/> to named source files and asserts the resulting
/// set of files equals the expected fixed sources. Build properties are injected through a global analyzer
/// config, mirroring <see cref="SingleNamespacePerFileVerifier"/>.
/// </summary>
internal static class SingleNamespacePerFileCodeFixVerifier
{
    public static async Task VerifyAsync(
        (string Name, string Content)[] sources,
        (string Name, string Content)[] fixedSources,
        params (string Key, string Value)[] properties
    )
    {
        var test = new CSharpCodeFixTest<
            SingleNamespacePerFileAnalyzer,
            SingleNamespacePerFileCodeFixProvider,
            DefaultVerifier
        >
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        foreach (var (name, content) in sources)
        {
            test.TestState.Sources.Add((name, content));
        }

        foreach (var (name, content) in fixedSources)
        {
            test.FixedState.Sources.Add((name, content));
        }

        if (properties.Length > 0)
        {
            var builder = new StringBuilder("is_global = true\n");
            foreach (var (key, value) in properties)
            {
                _ = builder.Append("build_property.").Append(key).Append(" = ").Append(value).Append('\n');
            }

            // Declare the global config in both states: the fix carries it into the fixed solution, so the
            // expected FixedState must contain it too, otherwise the analyzer-config comparison fails.
            var config = builder.ToString();
            test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", config));
            test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", config));
        }

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
