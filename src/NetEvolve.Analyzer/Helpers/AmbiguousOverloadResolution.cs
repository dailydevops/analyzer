namespace NetEvolve.Analyzer.Helpers;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Shared logic for NE0012: which suffixes are legitimately correct for a numeric literal, accounting for the
/// case where its converted type was picked by overload resolution among sibling overloads that disagree on
/// the numeric type at that position.
/// </summary>
internal static class AmbiguousOverloadResolution
{
    /// <summary>
    /// The set of suffixes that are correct for <paramref name="literal"/> — normally just
    /// <paramref name="requiredSuffix"/>, but every suffixable numeric type used by a sibling overload at the
    /// same argument position when <paramref name="literal"/> is an argument whose type was picked by overload
    /// resolution — e.g. one overload takes <see langword="long"/> and another <see langword="double"/>. Such
    /// a resolution is stable within one compilation, but the overload <em>set</em> it's resolved against can
    /// change between .NET versions (e.g. <c>TimeSpan.FromMinutes</c> gained a <see langword="long"/> overload
    /// in .NET 9 alongside its pre-existing <see langword="double"/> one). In a project that multi-targets
    /// both an old and a new target framework, only <paramref name="requiredSuffix"/> would need to differ per
    /// framework on the very same line — an unfixable, permanently recurring conflict if only that one suffix
    /// were ever accepted. Any of the returned suffixes is accepted instead of forcing one; a direct
    /// assignment/return (no overload resolution involved) is unaffected, since its type can't shift
    /// underneath it that way, and this returns just <paramref name="requiredSuffix"/> for it.
    /// </summary>
    public static ImmutableArray<string> GetValidSuffixes(
        SemanticModel semanticModel,
        LiteralExpressionSyntax literal,
        string requiredSuffix,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fallback = ImmutableArray.Create(requiredSuffix);

        if (
            literal.Parent is not ArgumentSyntax { NameColon: null } argument
            || argument.Parent is not BaseArgumentListSyntax { Parent: { } invocationOrCreation } argumentList
        )
        {
            return fallback;
        }

        // 'argument' is one of 'argumentList.Arguments' by construction (its Parent is argumentList), so it's
        // always found.
        var index = argumentList.Arguments.IndexOf(argument);

        if (
            semanticModel.GetSymbolInfo(invocationOrCreation, cancellationToken).Symbol
            is not IMethodSymbol resolvedMethod
        )
        {
            return fallback;
        }

        var candidates =
            resolvedMethod.MethodKind == MethodKind.Constructor
                ? resolvedMethod.ContainingType.Constructors
                : resolvedMethod.ContainingType.GetMembers(resolvedMethod.Name).OfType<IMethodSymbol>();

        var suffixes = candidates
            .Where(candidate => candidate.Parameters.Length > index)
            .Select(candidate => NumericLiteralSuffix.RequiredSuffix(candidate.Parameters[index].Type))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();

        return suffixes.Length > 1 ? suffixes : fallback;
    }
}
