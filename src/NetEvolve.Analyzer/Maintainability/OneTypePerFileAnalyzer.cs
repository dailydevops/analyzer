namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NE0001 — reports when a file declares more than one top-level type, or when its single top-level type
/// does not match the file name. Generic overloads that share a base name (<c>Result</c>, <c>Result&lt;T&gt;</c>)
/// are, by default, treated as distinct types encoded by arity (<c>Result{T}.cs</c>); enabling
/// <c>NetEvolveAnalyzerGroupGenericOverloads</c> lets them share a single file named after the base identifier.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneTypePerFileAnalyzer : DiagnosticAnalyzer
{
    private const string GroupGenericOverloadsProperty = "build_property.NetEvolveAnalyzerGroupGenericOverloads";
    private const string DisableProperty = "build_property.NetEvolveAnalyzerDisableFileOrganizationRules";
    private const string PublishSingleFileProperty = "build_property.PublishSingleFile";

    /// <summary>The descriptor for NE0001.</summary>
    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.NE0001,
        title: "Declare one type per file with a matching file name",
        messageFormat: "Type '{0}' should be declared in its own file named '{1}.cs'",
        category: DiagnosticCategories.Maintainability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each top-level type should live in its own file whose name matches the type. Generic "
            + "overloads are encoded by arity unless overload grouping is enabled.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.NE0001)
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

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
        if (GetBoolean(globalOptions, DisableProperty) || GetBoolean(globalOptions, PublishSingleFileProperty))
        {
            return;
        }

        var groupGenericOverloads = GetBoolean(globalOptions, GroupGenericOverloadsProperty);
        var root = context.Tree.GetRoot(context.CancellationToken);

        var groups = GroupTopLevelTypes(root, groupGenericOverloads);
        if (groups.Count == 0)
        {
            return;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var primary = groups.FirstOrDefault(group =>
            string.Equals(group.ExpectedFileName, fileName, StringComparison.Ordinal)
        );

        foreach (var group in groups)
        {
            if (ReferenceEquals(group, primary))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    group.First.Identifier.GetLocation(),
                    group.First.Display,
                    group.ExpectedFileName
                )
            );
        }
    }

    private static List<TypeGroup> GroupTopLevelTypes(SyntaxNode root, bool groupGenericOverloads)
    {
        var groups = new List<TypeGroup>();
        var index = new Dictionary<string, TypeGroup>(StringComparer.Ordinal);

        foreach (var node in root.DescendantNodes().Where(IsTopLevelTypeDeclaration))
        {
            var type = TypeDescriptor.From(node);

            // The identity key is scoped by namespace so that only genuine partial parts (same namespace,
            // name and arity) collapse into one group; two distinct same-named types in different namespaces
            // remain separate types and are each evaluated.
            var identity = groupGenericOverloads ? type.Name : type.MetadataName;
            var key = GetNamespaceName(node) + "::" + identity;
            if (!index.TryGetValue(key, out var group))
            {
                group = new TypeGroup(type, groupGenericOverloads);
                index.Add(key, group);
                groups.Add(group);
            }
        }

        return groups;
    }

    private static string GetNamespaceName(SyntaxNode node)
    {
        var segments = new List<string>();
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                segments.Add(namespaceDeclaration.Name.ToString());
            }
        }

        segments.Reverse();
        return string.Join(".", segments);
    }

    private static bool IsTopLevelTypeDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
        && node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;

    private static bool GetBoolean(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>A single top-level type declaration reduced to the facts NE0001 needs.</summary>
    private readonly struct TypeDescriptor
    {
        private TypeDescriptor(SyntaxToken identifier, string name, ImmutableArray<string> typeParameters)
        {
            Identifier = identifier;
            Name = name;
            TypeParameters = typeParameters;
        }

        public SyntaxToken Identifier { get; }

        public string Name { get; }

        public ImmutableArray<string> TypeParameters { get; }

        public string MetadataName =>
            TypeParameters.IsEmpty ? Name : Name + "`" + TypeParameters.Length.ToString(CultureInfo.InvariantCulture);

        public string Display => TypeParameters.IsEmpty ? Name : Name + "<" + string.Join(", ", TypeParameters) + ">";

        public string ArityEncodedFileName =>
            TypeParameters.IsEmpty ? Name : Name + "{" + string.Join(",", TypeParameters) + "}";

        public static TypeDescriptor From(SyntaxNode node)
        {
            if (node is TypeDeclarationSyntax type)
            {
                return new TypeDescriptor(
                    type.Identifier,
                    type.Identifier.ValueText,
                    GetTypeParameters(type.TypeParameterList)
                );
            }

            if (node is DelegateDeclarationSyntax @delegate)
            {
                return new TypeDescriptor(
                    @delegate.Identifier,
                    @delegate.Identifier.ValueText,
                    GetTypeParameters(@delegate.TypeParameterList)
                );
            }

            var @enum = (EnumDeclarationSyntax)node;
            return new TypeDescriptor(@enum.Identifier, @enum.Identifier.ValueText, ImmutableArray<string>.Empty);
        }

        private static ImmutableArray<string> GetTypeParameters(TypeParameterListSyntax? list) =>
            list is null
                ? ImmutableArray<string>.Empty
                : list.Parameters.Select(parameter => parameter.Identifier.ValueText).ToImmutableArray();
    }

    /// <summary>All declarations that share one type identity (partial parts, or grouped generic overloads).</summary>
    private sealed class TypeGroup
    {
        public TypeGroup(TypeDescriptor first, bool groupGenericOverloads)
        {
            First = first;
            ExpectedFileName = groupGenericOverloads ? first.Name : first.ArityEncodedFileName;
        }

        public TypeDescriptor First { get; }

        public string ExpectedFileName { get; }
    }
}
