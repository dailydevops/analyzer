namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0002 — reports when a file's top-level namespace declaration does not match the folder-derived namespace,
/// anchored at the <c>RootNamespace</c> MSBuild property. The expected value is <c>RootNamespace</c> joined with
/// the file's folder path relative to the project directory (see <see cref="FolderNamespace"/>). Files without a
/// namespace (the global namespace) and nested namespace declarations are out of scope.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamespaceMatchesFolderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property key carrying the expected folder-derived namespace.</summary>
    internal const string ExpectedNamespaceProperty = "ExpectedNamespace";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NamespaceMatchesFolder);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(AnalyzeTree);
    }

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
    {
        var filePath = context.Tree.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (
            GetBoolean(globalOptions, BuildProperty.DisableFileOrganizationRules)
            || GetBoolean(globalOptions, BuildProperty.PublishSingleFile)
        )
        {
            return;
        }

        if (!FolderNamespace.TryResolve(globalOptions, filePath, out var expected))
        {
            return;
        }

        var root = context.Tree.GetRoot(context.CancellationToken);

        // NE0002 evaluates only namespaces declared directly under the compilation unit. Nested namespaces
        // are left to NE0003, and source in the global namespace is out of scope.
        foreach (var node in root.ChildNodes())
        {
            if (node is not BaseNamespaceDeclarationSyntax declaration)
            {
                continue;
            }

            var actual = declaration.Name.ToString();
            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                continue;
            }

            // Surface the expected namespace so the code fix can rewrite the name without re-deriving it.
            var properties = ImmutableDictionary<string, string?>.Empty.Add(ExpectedNamespaceProperty, expected);

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.NamespaceMatchesFolder,
                    declaration.Name.GetLocation(),
                    properties,
                    actual,
                    expected
                )
            );
        }
    }

    private static bool GetBoolean(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
