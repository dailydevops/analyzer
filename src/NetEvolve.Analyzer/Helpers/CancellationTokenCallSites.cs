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
    /// appended to, or an expression that produces/consumes one without already being wired straight into a
    /// call — e.g. a hardcoded <c>CancellationToken.None</c>, which signals "no cancellation" rather than a
    /// real token, whether it sits bare or is passed on to a call. An expression that is itself a genuine token
    /// already passed directly as a call argument (e.g. an ambient <c>SomeContext.Current.CancellationToken</c>)
    /// does not count — that call site already has real cancellation support, so it isn't evidence the method
    /// still needs its own parameter. A method whose body has none of the above would end up with a parameter
    /// it can never actually use, so the analyzer uses this to skip it.
    /// </summary>
    public static bool HasUsableCancellationToken(SemanticModel semanticModel, MethodDeclarationSyntax method) =>
        HasUsableCancellationToken(semanticModel, method.Body, method.ExpressionBody?.Expression);

    private static bool HasUsableCancellationToken(
        SemanticModel semanticModel,
        BlockSyntax? body,
        ExpressionSyntax? expressionBodyExpression
    )
    {
        SyntaxNode? searchRoot = body is not null ? body : expressionBodyExpression;
        if (searchRoot is null)
        {
            return false;
        }

        return searchRoot
            .DescendantNodesAndSelf(descendIntoChildren: IsNotNestedFunctionScope)
            .Any(node =>
                node is InvocationExpressionSyntax invocation
                    ? TryGetAppendableParameterName(semanticModel, invocation, out _)
                    : node is ExpressionSyntax expression
                        && IsCancellationToken(semanticModel.GetTypeInfo(expression).Type)
                        && !IsNestedInCancellationTokenExpression(semanticModel, expression)
                        && !IsAlreadyWiredThroughArgument(semanticModel, expression)
            );
    }

    // Whether 'expression' is a sub-expression of a larger expression that is itself CancellationToken-typed
    // (e.g. the "CancellationToken" name half of a "Foo.CancellationToken" member access) — the outer
    // expression already carries the same signal, so this one would only be a redundant duplicate.
    private static bool IsNestedInCancellationTokenExpression(
        SemanticModel semanticModel,
        ExpressionSyntax expression
    ) => expression.Parent is ExpressionSyntax parent && IsCancellationToken(semanticModel.GetTypeInfo(parent).Type);

    // Whether 'expression' is passed straight into a call as an argument and is a genuine token (anything but
    // the hardcoded CancellationToken.None, which signals "no cancellation" rather than a real one) — evidence
    // the call already has a working token, not just something the method could still use one for.
    private static bool IsAlreadyWiredThroughArgument(SemanticModel semanticModel, ExpressionSyntax expression) =>
        expression.Parent is ArgumentSyntax && !IsCancellationTokenNone(semanticModel, expression);

    private static bool IsCancellationTokenNone(SemanticModel semanticModel, ExpressionSyntax expression) =>
        semanticModel.GetSymbolInfo(expression).Symbol
            is {
                Name: "None",
                ContainingType.Name: "CancellationToken",
                ContainingType.ContainingNamespace.Name: "Threading"
            };

    /// <summary>
    /// All call sites within <paramref name="method"/>'s body/expression-body that a
    /// <see cref="System.Threading.CancellationToken"/> argument could be appended to, paired with the name of
    /// the parameter it would be appended as (the invoked method's own parameter name, not the caller's).
    /// </summary>
    public static List<AppendableCallSite> CollectAppendableInvocations(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method
    ) => CollectAppendableInvocations(semanticModel, method.Body, method.ExpressionBody?.Expression);

    /// <summary>
    /// The same as the <see cref="MethodDeclarationSyntax"/> overload, but for a local function — used once a
    /// local function is itself being given a <see cref="System.Threading.CancellationToken"/> parameter (see
    /// <see cref="CollectLocalFunctionsNeedingCancellationToken"/>), so calls within its own body can be wired
    /// through the same way.
    /// </summary>
    public static List<AppendableCallSite> CollectAppendableInvocations(
        SemanticModel semanticModel,
        LocalFunctionStatementSyntax localFunction
    ) => CollectAppendableInvocations(semanticModel, localFunction.Body, localFunction.ExpressionBody?.Expression);

    private static List<AppendableCallSite> CollectAppendableInvocations(
        SemanticModel semanticModel,
        BlockSyntax? body,
        ExpressionSyntax? expressionBodyExpression
    )
    {
        SyntaxNode? searchRoot = body is not null ? body : expressionBodyExpression;
        if (searchRoot is null)
        {
            return [];
        }

        var callSites = new List<AppendableCallSite>();
        foreach (
            var invocation in searchRoot
                .DescendantNodesAndSelf(descendIntoChildren: IsNotNestedFunctionScope)
                .OfType<InvocationExpressionSyntax>()
        )
        {
            if (TryGetAppendableParameterName(semanticModel, invocation, out var parameterName))
            {
                callSites.Add(new AppendableCallSite(invocation, parameterName!));
            }
        }

        return callSites;
    }

    /// <summary>
    /// Local functions declared directly in <paramref name="method"/>'s body/expression-body — not nested
    /// inside a further lambda/anonymous method/local function — that return one of NE0010's supported async
    /// types, declare no <see cref="System.Threading.CancellationToken"/> parameter of their own, and have some
    /// actual use for one in their own body (see
    /// <see cref="HasUsableCancellationToken(SemanticModel, MethodDeclarationSyntax)"/>). Unlike an arbitrary
    /// external method, every call site of a local function is visible and rewritable in the same edit, so the
    /// NE0010 code fix extends these too instead of leaving them untouched.
    /// </summary>
    public static List<LocalFunctionStatementSyntax> CollectLocalFunctionsNeedingCancellationToken(
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
            .OfType<LocalFunctionStatementSyntax>()
            .Where(localFunction => NeedsCancellationTokenParameter(semanticModel, localFunction))
            .ToList();
    }

    private static bool NeedsCancellationTokenParameter(
        SemanticModel semanticModel,
        LocalFunctionStatementSyntax localFunction
    )
    {
        if (semanticModel.GetDeclaredSymbol(localFunction) is not IMethodSymbol symbol)
        {
            return false;
        }

        if (symbol.Parameters.Any(parameter => IsCancellationToken(parameter.Type)))
        {
            return false;
        }

        if (!IsSupportedAsyncReturnType(symbol.ReturnType))
        {
            return false;
        }

        return HasUsableCancellationToken(semanticModel, localFunction.Body, localFunction.ExpressionBody?.Expression);
    }

    private static readonly string[] SupportedAsyncReturnTypeMetadataNames =
    {
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.Task`1",
        "System.Threading.Tasks.ValueTask",
        "System.Threading.Tasks.ValueTask`1",
        "System.Collections.Generic.IAsyncEnumerable`1",
    };

    private static bool IsSupportedAsyncReturnType(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var original = namedType.OriginalDefinition;
        var fullName = $"{original.ContainingNamespace.ToDisplayString()}.{original.MetadataName}";
        return SupportedAsyncReturnTypeMetadataNames.Contains(fullName, System.StringComparer.Ordinal);
    }

    /// <summary>
    /// Invocations within <paramref name="method"/>'s body/expression-body — not nested inside a further
    /// lambda/anonymous method/local function — that resolve to <paramref name="target"/>. Used to find every
    /// call site of a local function the code fix is itself extending with a
    /// <see cref="System.Threading.CancellationToken"/> parameter (see
    /// <see cref="CollectLocalFunctionsNeedingCancellationToken"/>).
    /// </summary>
    public static List<InvocationExpressionSyntax> CollectTopLevelInvocationsOf(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method,
        IMethodSymbol target
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
            .Where(invocation =>
                SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(invocation).Symbol, target)
            )
            .ToList();
    }

    // Stops descent at a nested lambda/anonymous method/local function: a call inside one of those belongs to
    // a different (potentially already cancellation-aware) scope, not the enclosing method being inspected here.
    private static bool IsNotNestedFunctionScope(SyntaxNode node) =>
        node is not AnonymousFunctionExpressionSyntax && node is not LocalFunctionStatementSyntax;

    // Whether appending a named CancellationToken argument to 'invocation' is safe: either the resolved method
    // already has an unfilled CancellationToken parameter somewhere past the supplied arguments (not
    // necessarily last — anything trailing it is left at its default), or there is exactly one sibling
    // overload that adds one and every currently supplied argument would still line up positionally.
    // 'parameterName' comes out as the invoked method's own name for that parameter, since the argument is
    // appended by name specifically so it can target that slot even when other optional parameters follow it;
    // a positional append could only ever reach a truly last parameter. Existing named arguments are fine to
    // append alongside — C# requires them to already come after all positional ones, so the new trailing
    // named argument can only ever target a parameter nothing else has claimed. ref/out arguments are left
    // alone, since neither can appear on the CancellationToken slot itself and reasoning about them adds
    // nothing here.
    private static bool TryGetAppendableParameterName(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out string? parameterName
    )
    {
        parameterName = null;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Any(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None)))
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

        if (suppliedCount > parameters.Length)
        {
            return false;
        }

        if (TryFindSingleCancellationTokenParameterIndex(parameters, out var tokenIndex))
        {
            if (
                tokenIndex >= suppliedCount
                && AllOtherTrailingParametersOptional(parameters, suppliedCount, tokenIndex)
            )
            {
                parameterName = parameters[tokenIndex].Name;
                return true;
            }

            return false;
        }

        if (parameters.Any(parameter => IsCancellationToken(parameter.Type)))
        {
            // More than one CancellationToken parameter - ambiguous which slot the new token would target.
            return false;
        }

        return TryGetAppendableParameterNameFromSiblingOverload(
            invokedMethod,
            parameters,
            suppliedCount,
            out parameterName
        );
    }

    // Looks for exactly one sibling overload of 'invokedMethod' that adds a CancellationToken parameter the
    // current call could be redirected to (see IsAppendableOverload), and, if found, returns that parameter's
    // name.
    private static bool TryGetAppendableParameterNameFromSiblingOverload(
        IMethodSymbol invokedMethod,
        ImmutableArray<IParameterSymbol> parameters,
        int suppliedCount,
        out string? parameterName
    )
    {
        parameterName = null;

        var containingType = invokedMethod.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var matchingOverloads = containingType
            .GetMembers(invokedMethod.Name)
            .OfType<IMethodSymbol>()
            .Where(candidate => !SymbolEqualityComparer.Default.Equals(candidate, invokedMethod))
            .Where(candidate => IsAppendableOverload(candidate, parameters, suppliedCount))
            .Take(2)
            .ToList();

        if (
            matchingOverloads.Count != 1
            || !TryFindSingleCancellationTokenParameterIndex(matchingOverloads[0].Parameters, out var tokenIndex)
        )
        {
            return false;
        }

        parameterName = matchingOverloads[0].Parameters[tokenIndex].Name;
        return true;
    }

    // Whether 'candidate' is a sibling overload that adds exactly one CancellationToken parameter — anywhere
    // past the supplied arguments, not necessarily last — to 'originalParameters', with every other parameter
    // lining up by type and every parameter left without a supplied argument having a default value.
    private static bool IsAppendableOverload(
        IMethodSymbol candidate,
        ImmutableArray<IParameterSymbol> originalParameters,
        int suppliedCount
    )
    {
        if (candidate.Parameters.Length != originalParameters.Length + 1)
        {
            return false;
        }

        if (!TryFindSingleCancellationTokenParameterIndex(candidate.Parameters, out var tokenIndex))
        {
            return false;
        }

        return tokenIndex >= suppliedCount
            && ParametersMatchAroundInsertedToken(candidate.Parameters, originalParameters, tokenIndex)
            && AllOtherTrailingParametersOptional(candidate.Parameters, suppliedCount, tokenIndex);
    }

    // Finds the single CancellationToken-typed parameter in 'parameters'. Returns false (with index -1) both
    // when there is none and when there is more than one — the latter is ambiguous as to which slot a new
    // token argument would target, so callers treat it the same as "not found".
    private static bool TryFindSingleCancellationTokenParameterIndex(
        ImmutableArray<IParameterSymbol> parameters,
        out int index
    )
    {
        index = -1;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!IsCancellationToken(parameters[i].Type))
            {
                continue;
            }

            if (index >= 0)
            {
                index = -1;
                return false;
            }

            index = i;
        }

        return index >= 0;
    }

    // Whether every parameter at or past 'suppliedCount' — other than 'excludedIndex', which the caller is
    // about to fill by name — can safely be left unsupplied: it has a default value, or it's a trailing
    // 'params' parameter (necessarily the last one, and legal to omit entirely).
    private static bool AllOtherTrailingParametersOptional(
        ImmutableArray<IParameterSymbol> parameters,
        int suppliedCount,
        int excludedIndex
    )
    {
        for (var index = suppliedCount; index < parameters.Length; index++)
        {
            if (index != excludedIndex && !parameters[index].IsOptional && !parameters[index].IsParams)
            {
                return false;
            }
        }

        return true;
    }

    // Whether 'candidateParameters' equals 'originalParameters' with one CancellationToken parameter spliced
    // in at 'insertedIndex' — every other parameter, shifted by one once past that index, still matches by type.
    private static bool ParametersMatchAroundInsertedToken(
        ImmutableArray<IParameterSymbol> candidateParameters,
        ImmutableArray<IParameterSymbol> originalParameters,
        int insertedIndex
    )
    {
        for (var i = 0; i < originalParameters.Length; i++)
        {
            var candidateIndex = i < insertedIndex ? i : i + 1;
            if (
                !SymbolEqualityComparer.Default.Equals(
                    candidateParameters[candidateIndex].Type,
                    originalParameters[i].Type
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCancellationToken(ITypeSymbol? type) =>
        type is { Name: "CancellationToken", ContainingNamespace.Name: "Threading" };

    /// <summary>
    /// A call site a <see cref="System.Threading.CancellationToken"/> argument can be appended to, together
    /// with the name of the parameter it targets on the invoked method — used to append the argument by name
    /// rather than positionally.
    /// </summary>
    public readonly struct AppendableCallSite
    {
        public AppendableCallSite(InvocationExpressionSyntax invocation, string parameterName)
        {
            Invocation = invocation;
            ParameterName = parameterName;
        }

        public InvocationExpressionSyntax Invocation { get; }

        public string ParameterName { get; }
    }
}
