namespace NetEvolve.Analyzer.Helpers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Shared logic for NE0010 (<see cref="Usage.RequireCancellationTokenParameterAnalyzer"/>) and its code fix
/// (<see cref="Usage.RequireCancellationTokenParameterCodeFixProvider"/>): finds call sites within a method's
/// body/expression-body that a <see cref="System.Threading.CancellationToken"/> argument could be appended to
/// without changing which overload a caller ends up invoking.
/// </summary>
internal static class CancellationTokenCallSites
{
    /// <summary>
    /// Whether <paramref name="method"/>'s body/expression-body has any use for a
    /// <see cref="System.Threading.CancellationToken"/> parameter: either a call site the token could be
    /// appended to, or an expression that already produces/consumes one (e.g. a hardcoded
    /// <c>CancellationToken.None</c>) — evidence the method is already cancellation-aware even though nothing
    /// wires a real token through yet. A method whose body has neither would end up with a parameter it can
    /// never actually use, so the analyzer uses this to skip it.
    /// </summary>
    public static bool HasUsableCancellationToken(SemanticModel semanticModel, MethodDeclarationSyntax method)
    {
        SyntaxNode? searchRoot = method.Body is not null ? method.Body : method.ExpressionBody?.Expression;
        if (searchRoot is null)
        {
            return false;
        }

        return searchRoot
            .DescendantNodesAndSelf(descendIntoChildren: IsNotNestedFunctionScope)
            .Any(node =>
                node is InvocationExpressionSyntax invocation
                    ? IsAppendableCall(semanticModel, invocation)
                    : node is ExpressionSyntax expression
                        && IsCancellationToken(semanticModel.GetTypeInfo(expression).Type)
            );
    }

    /// <summary>
    /// All call sites within <paramref name="method"/>'s body/expression-body that a
    /// <see cref="System.Threading.CancellationToken"/> argument could be appended to.
    /// </summary>
    public static List<InvocationExpressionSyntax> CollectAppendableInvocations(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method
    ) => EnumerateAppendableInvocations(semanticModel, method).ToList();

    private static IEnumerable<InvocationExpressionSyntax> EnumerateAppendableInvocations(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method
    )
    {
        SyntaxNode? searchRoot = method.Body is not null ? method.Body : method.ExpressionBody?.Expression;
        if (searchRoot is null)
        {
            return [];
        }

        return searchRoot
            .DescendantNodesAndSelf(descendIntoChildren: IsNotNestedFunctionScope)
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsAppendableCall(semanticModel, invocation));
    }

    // Stops descent at a nested lambda/anonymous method/local function: a call inside one of those belongs to
    // a different (potentially already cancellation-aware) scope, not the enclosing method being inspected here.
    private static bool IsNotNestedFunctionScope(SyntaxNode node) =>
        node is not AnonymousFunctionExpressionSyntax && node is not LocalFunctionStatementSyntax;

    // Whether appending a 'cancellationToken' argument to 'invocation' is safe: either the resolved method
    // already ends with an unfilled CancellationToken parameter, or there is exactly one sibling overload that
    // adds one as its trailing parameter and every currently supplied argument would still line up
    // positionally. Named, ref, and out arguments are left alone entirely to keep this analysis simple.
    private static bool IsAppendableCall(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (
            arguments.Any(argument =>
                argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)
            )
        )
        {
            return false;
        }

        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invokedMethod)
        {
            return false;
        }

        if (arguments.Any(argument => IsCancellationToken(semanticModel.GetTypeInfo(argument.Expression).Type)))
        {
            // Already passing a token explicitly somewhere in the call.
            return false;
        }

        var parameters = invokedMethod.Parameters;
        var suppliedCount = arguments.Count;

        if (
            parameters.Length > 0
            && IsCancellationToken(parameters[parameters.Length - 1].Type)
            && suppliedCount == parameters.Length - 1
        )
        {
            return true;
        }

        if (parameters.Any(parameter => IsCancellationToken(parameter.Type)) || suppliedCount != parameters.Length)
        {
            return false;
        }

        var containingType = invokedMethod.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var matchingOverloads = containingType
            .GetMembers(invokedMethod.Name)
            .OfType<IMethodSymbol>()
            .Where(candidate => !SymbolEqualityComparer.Default.Equals(candidate, invokedMethod))
            .Where(candidate => candidate.Parameters.Length == parameters.Length + 1)
            .Where(candidate =>
                candidate.Parameters.Length > 0
                && IsCancellationToken(candidate.Parameters[candidate.Parameters.Length - 1].Type)
            )
            .Where(candidate => LeadingParameterTypesMatch(candidate.Parameters, parameters))
            .Take(2)
            .ToList();

        return matchingOverloads.Count == 1;
    }

    private static bool LeadingParameterTypesMatch(
        ImmutableArray<IParameterSymbol> longer,
        ImmutableArray<IParameterSymbol> shorter
    )
    {
        for (var index = 0; index < shorter.Length; index++)
        {
            if (!SymbolEqualityComparer.Default.Equals(longer[index].Type, shorter[index].Type))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCancellationToken(ITypeSymbol? type) =>
        type is { Name: "CancellationToken", ContainingNamespace.Name: "Threading" };
}
