namespace NetEvolve.Analyzer.Style;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NE0011 — flags every <c>#region</c>/<c>#endregion</c> directive pair. A single descriptor is used, but the
/// effective severity of each report varies by location: <see cref="DiagnosticSeverity.Warning"/> when the
/// <c>#region</c> sits inside the executable body of a method, constructor, accessor, operator, or local
/// function; <see cref="DiagnosticSeverity.Info"/> everywhere else (type level, namespace level, or file
/// level).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidRegionDirectivesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.AvoidRegionDirectives);

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
            if (!trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
            {
                continue;
            }

            if (trivia.GetStructure() is not RegionDirectiveTriviaSyntax regionDirective)
            {
                continue;
            }

            // Only a #region with a matching #endregion qualifies; an unterminated region (e.g. at end of
            // file due to a syntax error) has no related #endregion and is left alone.
            if (!regionDirective.GetRelatedDirectives().Any(related => related is EndRegionDirectiveTriviaSyntax))
            {
                continue;
            }

            var severity = DetermineSeverity(trivia);
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.AvoidRegionDirectives,
                    regionDirective.GetLocation(),
                    severity,
                    additionalLocations: null,
                    properties: null
                )
            );
        }
    }

    /// <summary>
    /// <see cref="DiagnosticSeverity.Warning"/> when the region's owning token has a <see cref="BlockSyntax"/>
    /// ancestor that belongs to a method, constructor, accessor, operator, or local function body — reached
    /// before hitting a <see cref="MemberDeclarationSyntax"/> boundary going up; otherwise
    /// <see cref="DiagnosticSeverity.Info"/>.
    /// </summary>
    private static DiagnosticSeverity DetermineSeverity(SyntaxTrivia regionTrivia)
    {
        for (var current = regionTrivia.Token.Parent; current is not null; current = current.Parent)
        {
            if (current is BlockSyntax block)
            {
                if (IsMemberBodyBlock(block))
                {
                    return DiagnosticSeverity.Warning;
                }

                continue;
            }

            if (current is MemberDeclarationSyntax)
            {
                break;
            }
        }

        return DiagnosticSeverity.Info;
    }

    private static bool IsMemberBodyBlock(BlockSyntax block) =>
        block.Parent
            is MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or DestructorDeclarationSyntax
                or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax;
}
