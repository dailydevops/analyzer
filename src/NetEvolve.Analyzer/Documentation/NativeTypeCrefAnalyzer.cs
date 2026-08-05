namespace NetEvolve.Analyzer.Documentation;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0008 — reports a <c>&lt;c&gt;</c> or <c>&lt;code&gt;</c> element inside an XML doc comment whose entire
/// content is a single recognized native/predefined C# type name (<see cref="CSharpKeywords.NativeTypeKeywords"/>)
/// or common BCL value type name (<see cref="CSharpKeywords.WellKnownBclTypeNames"/> — <c>Guid</c>,
/// <c>DateTime</c>, <c>DateTimeOffset</c>, <c>TimeSpan</c>), which should instead use
/// <c>&lt;see cref="..."/&gt;</c>. <c>DateOnly</c>/<c>TimeOnly</c> (<see cref="CSharpKeywords.ConditionalBclTypeNames"/>)
/// join the recognized set only when the compilation actually has the type in scope — a consumer targeting a
/// framework older than .NET 6 has no such type to <c>cref</c>, so flagging it there would be wrong. The whole
/// documentation-comment tree of a member is inspected, not just <c>&lt;summary&gt;</c> — the same mistake is
/// equally possible in <c>&lt;param&gt;</c>, <c>&lt;returns&gt;</c>, <c>&lt;value&gt;</c>,
/// <c>&lt;exception&gt;</c>, <c>&lt;remarks&gt;</c>, and <c>&lt;typeparam&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NativeTypeCrefAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BaseRecognizedTypeNames = CSharpKeywords.NativeTypeKeywords.Union(
        CSharpKeywords.WellKnownBclTypeNames
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NativeTypeCref);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(AnalyzeCompilationStart);
    }

    private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
    {
        var recognizedTypeNames = HasConditionalBclType(context.Compilation)
            ? BaseRecognizedTypeNames.Union(CSharpKeywords.ConditionalBclTypeNames)
            : BaseRecognizedTypeNames;

        context.RegisterSyntaxTreeAction(treeContext => AnalyzeTree(treeContext, recognizedTypeNames));
    }

    // DateOnly and TimeOnly only exist from .NET 6 onward; a compilation targeting an older framework has
    // neither type in scope, so treating their bare names as a doc-comment mistake there would be wrong.
    // Checking one is enough — both shipped together in the same release.
    private static bool HasConditionalBclType(Compilation compilation) =>
        compilation.GetTypeByMetadataName("System.DateOnly") is not null;

    private static void AnalyzeTree(SyntaxTreeAnalysisContext context, ImmutableHashSet<string> recognizedTypeNames)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (
                !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
            )
            {
                continue;
            }

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentationComment)
            {
                continue;
            }

            foreach (var element in documentationComment.DescendantNodes().OfType<XmlElementSyntax>())
            {
                AnalyzeElement(context, element, recognizedTypeNames);
            }
        }
    }

    private static void AnalyzeElement(
        SyntaxTreeAnalysisContext context,
        XmlElementSyntax element,
        ImmutableHashSet<string> recognizedTypeNames
    )
    {
        if (!IsCOrCodeElement(element))
        {
            return;
        }

        var typeName = GetSoleNativeTypeContent(element, recognizedTypeNames);
        if (typeName is null)
        {
            return;
        }

        var elementName = element.StartTag.Name.LocalName.ValueText;
        context.ReportDiagnostic(
            Diagnostic.Create(DiagnosticDescriptors.NativeTypeCref, element.GetLocation(), typeName, elementName)
        );
    }

    // Whether the element is a <c> or <code> element — the only element kinds NE0008 ever inspects.
    private static bool IsCOrCodeElement(XmlElementSyntax element) =>
        string.Equals(element.StartTag.Name.LocalName.ValueText, "c", StringComparison.Ordinal)
        || string.Equals(element.StartTag.Name.LocalName.ValueText, "code", StringComparison.Ordinal);

    // Only a <c>/<code> element whose entire trimmed content is exactly one recognized native type name
    // qualifies; a code snippet, expression, or any surrounding prose is left alone because it is not a bare
    // type-name reference.
    private static string? GetSoleNativeTypeContent(
        XmlElementSyntax element,
        ImmutableHashSet<string> recognizedTypeNames
    )
    {
        if (element.Content.Count != 1 || element.Content[0] is not XmlTextSyntax text)
        {
            return null;
        }

        var content = string.Concat(text.TextTokens.Select(token => token.ValueText)).Trim();

        return recognizedTypeNames.Contains(content) ? content : null;
    }
}
