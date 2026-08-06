namespace NetEvolve.Analyzer.Style;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0013 — flags a plain integer literal immediately cast to a suffixable numeric type (e.g. <c>(long)0</c>),
/// which should use that type's literal suffix instead (<c>0L</c>). Only a literal with no existing suffix and
/// no decimal point or exponent qualifies — rewriting a literal that already has a fractional part could
/// change its value once the cast (and its rounding) is gone, so those are left alone. Generated code is
/// skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseNumericLiteralSuffixOverCastAnalyzer : DiagnosticAnalyzer
{
    private static readonly char[] FractionalMarkers = { '.', 'e', 'E' };

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseNumericLiteralSuffixOverCast);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
    }

    private static void AnalyzeCast(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;
        if (
            cast.Expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal
        )
        {
            return;
        }

        var text = literal.Token.Text;
        var (digits, suffix) = NumericLiteralSuffix.SplitSuffix(text);
        if (suffix.Length > 0 || digits.IndexOfAny(FractionalMarkers) >= 0)
        {
            // Already suffixed, or has a fractional part/exponent — appending a suffix could change the
            // value once the cast's rounding is gone.
            return;
        }

        var castType = context.SemanticModel.GetTypeInfo(cast, context.CancellationToken).Type;
        var required = NumericLiteralSuffix.RequiredSuffix(castType);
        if (required is null)
        {
            return;
        }

        if (required is "D" or "F" or "M" && NumericLiteralSuffix.IsHexOrBinary(text))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.UseNumericLiteralSuffixOverCast,
                cast.GetLocation(),
                cast.ToString(),
                digits + required
            )
        );
    }
}
