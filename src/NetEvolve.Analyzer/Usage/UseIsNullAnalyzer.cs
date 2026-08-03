namespace NetEvolve.Analyzer.Usage;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0004 — reports a null comparison written with the equality operator (<c>x == null</c> or
/// <c>null == x</c>) that should use the <c>is null</c> pattern instead. Only the built-in equality is flagged:
/// a user-defined <c>operator ==</c> is left alone (the pattern would change semantics), as are comparisons
/// whose non-null operand cannot take a pattern (pointers, non-nullable value types) and comparisons inside a
/// LINQ expression tree, where the pattern does not compile. The diagnostic is raised regardless of the
/// project's language version; the accompanying code fix gates itself on the version.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseIsNullAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseIsNull);

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

        // Only the built-in '==' is a candidate; a user-defined operator carries its own semantics that the
        // 'is null' pattern would not preserve.
        if (operation.OperatorKind != BinaryOperatorKind.Equals || operation.OperatorMethod is not null)
        {
            return;
        }

        var leftIsNull = NullCheckOperand.IsNullLiteral(operation.LeftOperand);
        var rightIsNull = NullCheckOperand.IsNullLiteral(operation.RightOperand);

        // Exactly one side must be the null literal; both-null and neither-null are not null checks to rewrite.
        if (leftIsNull == rightIsNull)
        {
            return;
        }

        var operand = leftIsNull ? operation.RightOperand : operation.LeftOperand;
        if (!NullCheckOperand.IsPatternable(operand) || NullCheckOperand.IsWithinExpressionTree(operation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseIsNull, operation.Syntax.GetLocation()));
    }
}
