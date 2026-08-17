namespace NetEvolve.Analyzer.Style;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0012 — flags a numeric literal that doesn't carry the canonical, upper-case suffix matching the type
/// it's converted to (<c>0</c> assigned to a <see langword="long"/> should be <c>0L</c>, and likewise for
/// <see langword="ulong"/>/<c>UL</c>, <see langword="uint"/>/<c>U</c>, <see langword="float"/>/<c>F</c>,
/// <see langword="double"/>/<c>D</c>, and <see langword="decimal"/>/<c>M</c>). A hexadecimal or binary
/// integer literal converted to <see langword="float"/>, <see langword="double"/>, or <see langword="decimal"/>
/// is left alone — that format has no such suffix to add. A literal whose type was picked by overload
/// resolution among sibling overloads taking a different suffixable numeric type at the same position accepts
/// any of those types' suffixes, since that overload set (and thus the picked type) can differ between .NET
/// versions — see <see cref="AmbiguousOverloadResolution.GetValidSuffixes"/>. Its
/// code fix then offers one targeted action per accepted suffix instead of a single, possibly wrong one, with
/// no Fix All — each site needs its own, deliberate pick. Generated code is skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequireNumericLiteralSuffixAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RequireNumericLiteralSuffix);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.NumericLiteralExpression);
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var convertedType = context.SemanticModel.GetTypeInfo(literal, context.CancellationToken).ConvertedType;
        var required = NumericLiteralSuffix.RequiredSuffix(convertedType);
        if (required is null)
        {
            return;
        }

        var text = literal.Token.Text;
        if (required is "D" or "F" or "M" && NumericLiteralSuffix.IsHexOrBinary(text))
        {
            // A hex/binary literal has no floating-point or decimal form to mark with a suffix.
            return;
        }

        var (digits, suffix) = NumericLiteralSuffix.SplitSuffix(text);
        var validSuffixes = AmbiguousOverloadResolution.GetValidSuffixes(
            context.SemanticModel,
            literal,
            required,
            context.CancellationToken
        );
        if (validSuffixes.Contains(suffix, StringComparer.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.RequireNumericLiteralSuffix,
                literal.GetLocation(),
                text,
                digits + required
            )
        );
    }
}
