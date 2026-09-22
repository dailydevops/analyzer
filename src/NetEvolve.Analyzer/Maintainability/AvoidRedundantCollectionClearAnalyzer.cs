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
/// NE0015 — reports a parameterless <c>Clear()</c> call on a local collection variable that was declared with
/// a parameterless collection constructor (no copy-constructor argument, no collection initializer) and whose
/// first reference after that declaration is the <c>Clear()</c> call itself. Such a call has no effect, since
/// the instance is still empty at that point.
/// </summary>
/// <remarks>
/// Scope is deliberately narrow to avoid removing a call that could have a side effect:
/// <list type="bullet">
/// <item>Only locals are considered; a field's lifetime and aliasing are out of scope.</item>
/// <item>The receiver's type must be exactly one of a fixed set of BCL collection types (see
/// <see cref="AllowedCollectionMetadataNames"/>), checked by original-definition equality rather than
/// interface implementation. A subclass — e.g. a <see cref="System.Collections.ObjectModel.Collection{T}"/>
/// override of <c>ClearItems()</c> that logs or disposes — is deliberately excluded, since its <c>Clear()</c>
/// behavior is no longer just "empty the list".</item>
/// <item>A copy-constructor argument (<c>new List&lt;T&gt;(other)</c>) or a non-empty collection initializer
/// (<c>new List&lt;T&gt; { 1, 2 }</c>) populates the instance at construction, so <c>Clear()</c> afterwards is
/// meaningful and is left alone.</item>
/// <item>The <c>Clear()</c> call itself must not sit inside a loop the declaration is not also inside (a
/// reference to the local <em>after</em> the call, inside a loop the declaration isn't in, cannot have
/// populated the instance before the call ran, so that case is still reported). Additionally, neither the
/// call nor any other reference to the local may sit inside a lambda, an anonymous method, or a local
/// function, where source order no longer implies execution order. The same applies if the enclosing block
/// contains a <see langword="goto"/> or a label.</item>
/// </list>
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

    // A bare 'x.Clear();' invocation with no arguments; anything else (used as an argument, a lambda's
    // expression body, etc.) is left alone, since removing it outright would not be safe.
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

    // The invoked method must be a genuine parameterless 'Clear()', the receiver a local, and that local's
    // type a collection — a type with an unrelated, possibly side-effecting 'Clear()' is never flagged.
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

    // The local must be declared exactly once, with a parameterless constructor call and no collection
    // initializer — either would mean the instance is already populated, so 'Clear()' is meaningful.
    private static BlockSyntax? GetDeclaringBlock(ILocalSymbol local, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A local symbol always has exactly one declaring syntax reference — its single declaration site —
        // so indexing straight into it is safe; only its syntax shape (a plain 'T x = new(...)' declarator
        // with an initializer) needs checking.
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

        // A local declaration always sits directly inside a 'VariableDeclarationSyntax' inside a
        // 'LocalDeclarationStatementSyntax'; that statement's own parent is the block the local is scoped to.
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

    // The 'Clear()' receiver must be the very first reference to the local after its declaration (an earlier
    // reference means the instance may already hold data), and neither it nor any other reference may sit
    // inside a loop, a lambda, an anonymous method, or a local function, where source order no longer implies
    // execution order. A goto/label anywhere in the block can likewise jump past or back over either
    // statement, so its mere presence disqualifies the whole declaration.
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

        // A loop around the receiver itself could run the 'Clear()' call more than once; a loop around a
        // *later* reference cannot have populated the instance before this call already ran, so that case is
        // still safe to report.
        if (CrossesBoundary(receiver, declaringBlock, IsLoop))
        {
            return false;
        }

        // A lambda/anonymous method/local function can be invoked at any time relative to its surrounding
        // source position, for any reference — including the receiver itself, if it sits inside one.
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

    // Exact BCL collection types only, matched by original-definition equality rather than interface
    // implementation: a user subclass overriding the virtual 'ClearItems()' hook (e.g. a logging or
    // disposing 'Collection<T>') would otherwise also match 'ICollection<T>', yet its 'Clear()' can carry
    // arbitrary side effects that this rule must not delete.
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
