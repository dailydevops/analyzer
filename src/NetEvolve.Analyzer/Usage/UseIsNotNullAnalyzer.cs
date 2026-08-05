namespace NetEvolve.Analyzer.Usage;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0005 — reports an inequality comparison against <see langword="null"/> (<c>x != null</c> or <c>null != x</c>) that
/// should be written with the <c>is not null</c> pattern. The pattern always performs a null check and cannot be
/// redefined by a user-defined <c>operator !=</c>, so it states the intent unambiguously. The comparison is only
/// flagged when a pattern form is legal and equivalent: the non-null operand must be patternable (a non-pointer
/// reference type, <see cref="System.Nullable{T}"/>, or an unconstrained type parameter), the comparison must not
/// resolve to a user-defined operator, and the expression must not sit inside a LINQ expression tree.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseIsNotNullAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseIsNotNull);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeBinary, OperationKind.Binary);
    }

    private static void AnalyzeBinary(OperationAnalysisContext context)
    {
        var operation = (IBinaryOperation)context.Operation;
        if (operation.OperatorKind != BinaryOperatorKind.NotEquals)
        {
            return;
        }

        // A user-defined 'operator !=' has its own semantics, so the pattern form is not an equivalent rewrite.
        if (operation.OperatorMethod is not null)
        {
            return;
        }

        var leftIsNull = NullCheckOperand.IsNullLiteral(operation.LeftOperand);
        var rightIsNull = NullCheckOperand.IsNullLiteral(operation.RightOperand);

        // Exactly one side must be the null literal; the other is the value being null-checked.
        if (leftIsNull == rightIsNull)
        {
            return;
        }

        var operand = leftIsNull ? operation.RightOperand : operation.LeftOperand;
        if (!NullCheckOperand.IsPatternable(operand))
        {
            return;
        }

        if (NullCheckOperand.IsWithinExpressionTree(operation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseIsNotNull, operation.Syntax.GetLocation()));
    }
}
