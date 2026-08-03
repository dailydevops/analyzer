namespace NetEvolve.Analyzer.Tests.Unit.Maintainability;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using NetEvolve.Analyzer.Maintainability;

/// <summary>
/// Runs <see cref="NamespaceMatchesFolderAnalyzer"/> against one or more <b>named</b> source files (the analyzer
/// is file-path sensitive) and, optionally, a set of MSBuild build properties injected through a global analyzer
/// config. Diagnostics are declared inline with <c>{|NE0002:Namespace|}</c> markup.
/// </summary>
internal static class NamespaceMatchesFolderVerifier
{
    public static async Task VerifyAsync(
        (string Name, string Content)[] sources,
        params (string Key, string Value)[] properties
    )
    {
        var test = new CSharpAnalyzerTest<NamespaceMatchesFolderAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        foreach (var (name, content) in sources)
        {
            test.TestState.Sources.Add((name, content));
        }

        if (properties.Length > 0)
        {
            var builder = new StringBuilder("is_global = true\n");
            foreach (var (key, value) in properties)
            {
                _ = builder.Append("build_property.").Append(key).Append(" = ").Append(value).Append('\n');
            }

            var config = builder.ToString();
            test.SolutionTransforms.Add(
                (solution, projectId) =>
                    solution.AddAnalyzerConfigDocument(
                        DocumentId.CreateNewId(projectId),
                        ".globalconfig",
                        SourceText.From(config),
                        filePath: "/.globalconfig"
                    )
            );
        }

        await test.RunAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Convenience overload for a single named source file.</summary>
    public static Task VerifyAsync(string name, string content, params (string Key, string Value)[] properties) =>
        VerifyAsync([(name, content)], properties);
}
