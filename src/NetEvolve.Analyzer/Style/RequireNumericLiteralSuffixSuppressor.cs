namespace NetEvolve.Analyzer.Style;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Abstractions;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NES0002 — suppresses SonarAnalyzer's <c>S818</c> ("Literal suffixes should be upper case") wherever
/// <see cref="RequireNumericLiteralSuffixAnalyzer">NE0012</see> already reports the same numeric literal, so
/// a consuming project running both analyzers doesn't get two conflicting fixes for the same literal. This
/// only happens when the literal's suffix has both the wrong letter and the wrong case (e.g. <c>0u</c> where
/// a <see langword="long"/> is expected) — S818 still fires on its own when only the case is wrong (e.g.
/// <c>0l</c> where <see langword="long"/> is expected), since NE0012 normalizes case before comparing and
/// doesn't flag that.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RequireNumericLiteralSuffixSuppressor : NetEvolveSuppressorBase
{
    internal static readonly SuppressionDescriptor Suppression = new(
        id: DiagnosticIds.NES0002,
        suppressedDiagnosticId: "S818",
        justification: "NE0012 already reports this numeric literal; suppressed to avoid a duplicate, "
            + "conflicting diagnostic from SonarAnalyzer.CSharp."
    );

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(Suppression);

    /// <inheritdoc />
    protected override bool ShouldSuppress(
        Diagnostic diagnostic,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tree = diagnostic.Location.SourceTree;
        if (tree is null)
        {
            return false;
        }

        var root = tree.GetRoot(cancellationToken);
        var literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<LiteralExpressionSyntax>()
            .FirstOrDefault();
        if (literal is null)
        {
            return false;
        }

        // RS1030: this suppressor runs once per already-reported S818 diagnostic, not per node during a
        // syntax/semantic walk, so the incremental-caching concern the rule guards against doesn't apply here.
#pragma warning disable RS1030
        var semanticModel = compilation.GetSemanticModel(tree);
#pragma warning restore RS1030
        var required = NumericLiteralSuffix.RequiredSuffix(
            semanticModel.GetTypeInfo(literal, cancellationToken).ConvertedType
        );
        if (required is null)
        {
            return false;
        }

        var (_, suffix) = NumericLiteralSuffix.SplitSuffix(literal.Token.Text);
        var validSuffixes = AmbiguousOverloadResolution.GetValidSuffixes(
            semanticModel,
            literal,
            required,
            cancellationToken
        );
        return !validSuffixes.Contains(suffix, StringComparer.Ordinal);
    }
}
