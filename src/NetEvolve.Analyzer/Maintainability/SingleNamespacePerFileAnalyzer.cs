namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NE0003 — reports when a file declares more than one namespace. All namespace declarations (block- and
/// file-scoped, including nested) are collected in document order; when there is more than one, every
/// declaration except the first is flagged, so a file always narrows to a single namespace.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleNamespacePerFileAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property key: <c>"true"</c> when the flagged namespace is nested inside another.</summary>
    internal const string NestedProperty = "Nested";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.SingleNamespacePerFile);

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
        var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (
            GetBoolean(globalOptions, BuildProperty.DisableFileOrganizationRules)
            || GetBoolean(globalOptions, BuildProperty.PublishSingleFile)
        )
        {
            return;
        }

        var root = context.Tree.GetRoot(context.CancellationToken);

        // Document order (pre-order) puts a parent namespace before the child it contains, so the first
        // declaration is always the outermost one and is the one we keep.
        var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToList();
        if (namespaces.Count <= 1)
        {
            return;
        }

        for (var index = 1; index < namespaces.Count; index++)
        {
            var declaration = namespaces[index];

            // Surface whether the flagged declaration is nested so the code fix offers flatten only for the
            // nested shape; the sibling shape is left to NE0001's move-type fix.
            var nested = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Any();
            var value = nested ? "true" : "false";
            var properties = ImmutableDictionary<string, string?>.Empty.Add(NestedProperty, value);

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.SingleNamespacePerFile,
                    declaration.Name.GetLocation(),
                    properties
                )
            );
        }
    }

    private static bool GetBoolean(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
