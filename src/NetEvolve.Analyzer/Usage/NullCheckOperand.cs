namespace NetEvolve.Analyzer.Usage;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Shared operand analysis for the null-check idiom rules (NE0004–NE0006). Determines whether an operand is the
/// <c>null</c> literal and whether a pattern-based null check (<c>is null</c> / <c>is not null</c>) can legally
/// and equivalently replace a comparison against it. Pointer operands (for which the pattern does not compile)
/// and non-nullable value types (for which the comparison is not a null check) are excluded here, so the
/// analyzers never report a diagnostic that has no valid pattern form.
/// </summary>
internal static class NullCheckOperand
{
    /// <summary>
    /// Whether <paramref name="operation"/>, after unwrapping an implicit conversion, is the <c>null</c> literal.
    /// </summary>
    public static bool IsNullLiteral(IOperation operation) =>
        Unwrap(operation) is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };

    /// <summary>
    /// Whether a pattern-based null check can replace a comparison against <c>null</c> for
    /// <paramref name="operand"/>: its static type must be a non-pointer reference type, a
    /// <see cref="System.Nullable{T}"/>, or an unconstrained type parameter. Non-nullable value types and pointer
    /// types are rejected.
    /// </summary>
    public static bool IsPatternable(IOperation operand) =>
        Unwrap(operand).Type switch
        {
            null => false,
            { TypeKind: TypeKind.Pointer } => false,
            { IsReferenceType: true } => true,
            INamedTypeSymbol named => named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
            // An unconstrained type parameter (no 'struct' constraint) may be a reference type at runtime, so
            // the pattern is legal; a 'where T : struct' parameter can never be null and is excluded.
            ITypeParameterSymbol parameter => !parameter.HasValueTypeConstraint,
            _ => false,
        };

    /// <summary>
    /// Whether <paramref name="operation"/> sits inside a LINQ expression-tree lambda (<c>Expression&lt;T&gt;</c>),
    /// where the <c>is</c> pattern is not allowed and a rewrite would not compile.
    /// </summary>
    public static bool IsWithinExpressionTree(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation && current.Parent?.Type is { } target && IsExpression(target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExpression(ITypeSymbol? type) =>
        type is not null
        && (
            (
                type is INamedTypeSymbol { Name: "Expression", ContainingNamespace: { Name: "Expressions" } linq }
                && linq.ContainingNamespace is { Name: "Linq", ContainingNamespace.Name: "System" }
            ) || IsExpression(type.BaseType)
        );

    private static IOperation Unwrap(IOperation operation) =>
        operation is IConversionOperation conversion ? conversion.Operand : operation;
}
