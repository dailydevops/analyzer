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
/// NE0007 — reports a <c>&lt;c&gt;</c> element inside an XML doc comment whose entire content is a single
/// recognized C# keyword (<see langword="true"/>, <see langword="false"/>, <see langword="null"/>, ...), which
/// should instead use <c>&lt;see langword="..."/&gt;</c>. The whole documentation-comment tree of a member is
/// inspected, not just <c>&lt;summary&gt;</c> — the same mistake is equally possible in <c>&lt;param&gt;</c>,
/// <c>&lt;returns&gt;</c>, <c>&lt;value&gt;</c>, <c>&lt;exception&gt;</c>, <c>&lt;remarks&gt;</c>, and
/// <c>&lt;typeparam&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseLangwordAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Every recognized C# keyword (<see cref="CSharpKeywords.ReservedKeywords"/> and
    /// <see cref="CSharpKeywords.ContextualKeywords"/>) worth flagging when it is the sole content of a
    /// <c>&lt;c&gt;</c> element.
    /// </summary>
    internal static readonly ImmutableHashSet<string> Keywords = CSharpKeywords.ReservedKeywords.Union(
        CSharpKeywords.ContextualKeywords
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseLangword);

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
                AnalyzeElement(context, element);
            }
        }
    }

    private static void AnalyzeElement(SyntaxTreeAnalysisContext context, XmlElementSyntax element)
    {
        if (!string.Equals(element.StartTag.Name.LocalName.ValueText, "c", StringComparison.Ordinal))
        {
            return;
        }

        var keyword = GetSoleKeywordContent(element);
        if (keyword is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseLangword, element.GetLocation(), keyword));
    }

    // Only a <c> element whose entire trimmed content is exactly one recognized keyword qualifies; a code
    // snippet, expression, or any surrounding prose (e.g. "<c>x == null</c>", "<c>the true value</c>") is left
    // alone because it is not a bare keyword reference.
    private static string? GetSoleKeywordContent(XmlElementSyntax element)
    {
        if (element.Content.Count != 1 || element.Content[0] is not XmlTextSyntax text)
        {
            return null;
        }

        var content = string.Concat(text.TextTokens.Select(token => token.ValueText)).Trim();

        return Keywords.Contains(content) ? content : null;
    }
}
