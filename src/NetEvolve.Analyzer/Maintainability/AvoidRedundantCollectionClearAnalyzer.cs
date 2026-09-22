namespace NetEvolve.Analyzer.Maintainability;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NE0015 — reports a parameterless <c>Clear()</c> call on a local collection that was constructed with no
/// arguments and no initializer, where the call is the first reference to that local. Such a call is a no-op.
/// </summary>
/// <remarks>
/// Locals only, and only for a fixed set of BCL collection types (<see cref="AllowedCollectionMetadataNames"/>)
/// matched by exact type identity, so a <see cref="System.Collections.ObjectModel.Collection{T}"/> subclass
/// overriding <c>ClearItems()</c> is never touched. A loop around the <c>Clear()</c> call itself, a lambda, an
/// anonymous method, or a local function anywhere in scope, or a <see langword="goto"/>/label in the
/// enclosing block all disqualify the declaration, since source order then no longer implies execution order.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidRedundantCollectionClearAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.AvoidRedundantCollectionClear);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetClearReceiver(invocation) is not { } receiver)
        {
            return;
        }

        var cancellationToken = context.CancellationToken;
        var semanticModel = context.SemanticModel;

        if (GetEmptyCollectionLocal(invocation, receiver, semanticModel, cancellationToken) is not { } local)
        {
            return;
        }

        if (GetDeclaringBlock(local, cancellationToken) is not { } declaringBlock)
        {
            return;
        }

        if (!IsFirstAndOnlySafeUse(declaringBlock, receiver, local, semanticModel, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(DiagnosticDescriptors.AvoidRedundantCollectionClear, invocation.GetLocation(), local.Name)
        );
    }

    private static IdentifierNameSyntax? GetClearReceiver(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 0 || invocation.Parent is not ExpressionStatementSyntax)
        {
            return null;
        }

        if (
            invocation.Expression
                is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Clear" } memberAccess
            || memberAccess.Expression is not IdentifierNameSyntax receiver
        )
        {
            return null;
        }

        return receiver;
    }

    private static ILocalSymbol? GetEmptyCollectionLocal(
        InvocationExpressionSyntax invocation,
        IdentifierNameSyntax receiver,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (
            semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol
            is not IMethodSymbol { Parameters.Length: 0 }
        )
        {
            return null;
        }

        if (semanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol is not ILocalSymbol local)
        {
            return null;
        }

        return IsAllowedCollectionType(local.Type, semanticModel.Compilation) ? local : null;
    }

    private static BlockSyntax? GetDeclaringBlock(ILocalSymbol local, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A local symbol always has exactly one declaring syntax reference.
        if (
            local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken)
            is not VariableDeclaratorSyntax { Initializer.Value: { } initializerValue } declarator
        )
        {
            return null;
        }

        if (GetCreationShape(initializerValue) is not (var arguments, var initializer))
        {
            return null;
        }

        if (arguments is { Arguments.Count: > 0 } || initializer is { Expressions.Count: > 0 })
        {
            return null;
        }

        return declarator.Parent!.Parent is LocalDeclarationStatementSyntax { Parent: BlockSyntax declaringBlock }
            ? declaringBlock
            : null;
    }

    private static (ArgumentListSyntax? Arguments, InitializerExpressionSyntax? Initializer)? GetCreationShape(
        ExpressionSyntax expression
    ) =>
        expression switch
        {
            ObjectCreationExpressionSyntax objectCreation => (objectCreation.ArgumentList, objectCreation.Initializer),
            ImplicitObjectCreationExpressionSyntax implicitCreation => (
                implicitCreation.ArgumentList,
                implicitCreation.Initializer
            ),
            _ => null,
        };

    private static bool IsFirstAndOnlySafeUse(
        BlockSyntax declaringBlock,
        IdentifierNameSyntax receiver,
        ILocalSymbol local,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (declaringBlock.DescendantNodes().Any(node => node is GotoStatementSyntax or LabeledStatementSyntax))
        {
            return false;
        }

        var references = declaringBlock
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    local
                )
            )
            .OrderBy(identifier => identifier.SpanStart)
            .ToList();

        if (references.Count == 0 || references[0].SpanStart != receiver.SpanStart)
        {
            return false;
        }

        // Only the receiver's own loop membership matters: a loop around a later reference can't have
        // populated the instance before this call already ran.
        if (CrossesBoundary(receiver, declaringBlock, IsLoop))
        {
            return false;
        }

        return !references.Any(identifier => CrossesBoundary(identifier, declaringBlock, IsDeferredScope));
    }

    private static bool CrossesBoundary(SyntaxNode node, SyntaxNode boundary, Func<SyntaxNode, bool> isMatch)
    {
        for (var current = node.Parent; current is not null && current != boundary; current = current.Parent)
        {
            if (isMatch(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLoop(SyntaxNode node) =>
        node
            is ForStatementSyntax
                or ForEachStatementSyntax
                or ForEachVariableStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax;

    private static bool IsDeferredScope(SyntaxNode node) =>
        node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;

    // Exact-type match, not interface implementation: a subclass overriding 'ClearItems()' also implements
    // 'ICollection<T>' but its 'Clear()' can carry side effects this rule must not delete.
    private static readonly string[] AllowedCollectionMetadataNames = new[]
    {
        "System.Collections.ArrayList",
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.ObjectModel.Collection`1",
        "System.Collections.ObjectModel.ObservableCollection`1",
    };

    private static bool IsAllowedCollectionType(ITypeSymbol type, Compilation compilation) =>
        AllowedCollectionMetadataNames
            .Select(compilation.GetTypeByMetadataName)
            .Any(candidate =>
                candidate is not null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate)
            );
}
