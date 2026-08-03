namespace NetEvolve.Analyzer.Usage;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0006 — reports an <c>x is object</c> type check that is really a null check and should be written with the
/// <c>is not null</c> pattern, which states the intent directly instead of being misread as a type test. The
/// expression is only flagged when the pattern form is legal and equivalent: the checked value must be
/// patternable (a non-pointer reference type, <see cref="System.Nullable{T}"/>, or an unconstrained type
/// parameter), so a non-nullable value type — for which <c>is object</c> is always true — and pointers are
/// skipped, and the expression must not sit inside a LINQ expression tree. A declaration pattern such as
/// <c>x is object o</c> is an <see cref="IIsPatternOperation"/> rather than an <see cref="IIsTypeOperation"/>
/// and is therefore naturally excluded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseIsNotNullOverIsObjectAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.UseIsNotNullOverIsObject);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeIsType, OperationKind.IsType);
    }

    private static void AnalyzeIsType(OperationAnalysisContext context)
    {
        var operation = (IIsTypeOperation)context.Operation;

        // `x is not object` (the negated form) is not a non-null check, so it is out of scope.
        if (operation.IsNegated)
        {
            return;
        }

        // Only the bare `is object` reads as a null check; `is SomeType` is a genuine type test.
        if (operation.TypeOperand.SpecialType != SpecialType.System_Object)
        {
            return;
        }

        // A non-nullable value type (where `is object` is always true) or a pointer has no equivalent pattern.
        if (!NullCheckOperand.IsPatternable(operation.ValueOperand))
        {
            return;
        }

        if (NullCheckOperand.IsWithinExpressionTree(operation))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(DiagnosticDescriptors.UseIsNotNullOverIsObject, operation.Syntax.GetLocation())
        );
    }
}
