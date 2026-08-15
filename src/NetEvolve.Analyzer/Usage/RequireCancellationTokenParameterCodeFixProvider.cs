namespace NetEvolve.Analyzer.Usage;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// Code fix for <see cref="RequireCancellationTokenParameterAnalyzer">NE0010</see>. Appends
/// <c>CancellationToken cancellationToken = default</c> as the last parameter of the flagged method — or, if
/// the method declares a <see langword="params"/> parameter, inserts it right before that parameter instead,
/// since <see langword="params"/> must stay last — adding a <c>using System.Threading;</c> directive when the
/// file does not already have one in scope, and — on a
/// best-effort basis — passes the new token through to call sites within the method's own body that either
/// already have an unfilled <c>CancellationToken</c> parameter or have exactly one sibling overload adding
/// one. The token is always appended as a named argument (<c>cancellationToken: cancellationToken</c>) rather
/// than positionally, so the target parameter can be reached even when other optional parameters follow it.
/// A local function declared in the method's own body that still has a use for a token but none of its own is
/// extended the same way — with a parameter named <c>token</c> instead, since a local function's parameter
/// cannot share a name with anything already in scope in its enclosing method (CS0136) — and every call site of
/// it found at the enclosing method's own top level is updated to pass it along; a nested lambda is left alone,
/// since its own cancellation handling is out of this fix's scope.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequireCancellationTokenParameterCodeFixProvider))]
[Shared]
public sealed class RequireCancellationTokenParameterCodeFixProvider : CodeFixProvider
{
    private const string SystemThreadingNamespace = "System.Threading";
    private const string ParameterName = "cancellationToken";
    private const string LocalFunctionParameterName = "token";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0010);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add CancellationToken parameter",
                cancellationToken =>
                    AddCancellationTokenParameterAsync(context.Document, methodDeclaration, cancellationToken),
                equivalenceKey: "NE0010.RequireCancellationTokenParameter"
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddCancellationTokenParameterAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Re-find the method in the freshly fetched root so the replaced node belongs to that same tree.
        var currentMethod = root.FindNode(methodDeclaration.Span)
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var methodWithPropagatedCalls = semanticModel is null
            ? currentMethod
            : PropagateToCallSitesAndLocalFunctions(semanticModel, currentMethod);

        var parameter = CreateCancellationTokenParameter(ParameterName);
        var updatedParameterList = InsertParameter(methodWithPropagatedCalls.ParameterList, parameter);

        var newRoot = root.ReplaceNode(
            currentMethod,
            methodWithPropagatedCalls.WithParameterList(updatedParameterList)
        );
        newRoot = EnsureSystemThreadingUsing(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private static ParameterSyntax CreateCancellationTokenParameter(string name) =>
        SyntaxFactory
            .Parameter(SyntaxFactory.Identifier(name))
            .WithType(SyntaxFactory.IdentifierName("CancellationToken").WithTrailingTrivia(SyntaxFactory.Space))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))
            );

    // Passes the new token through to call sites within the method's own body/expression-body that can accept
    // it without changing which overload is chosen out from under the caller (see IsAppendableCall), and
    // extends any local function still needing one of its own (see
    // CollectLocalFunctionsNeedingCancellationToken) the same way. A nested lambda is skipped — capturing the
    // outer parameter there would be legal, but whether it's *wanted* is a judgment call this mechanical fix
    // doesn't make.
    private static MethodDeclarationSyntax PropagateToCallSitesAndLocalFunctions(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method
    )
    {
        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (var candidate in CancellationTokenCallSites.CollectAppendableInvocations(semanticModel, method))
        {
            replacements[candidate.Invocation] = AppendNamedArgument(
                candidate.Invocation,
                ParameterName,
                candidate.ParameterName
            );
        }

        foreach (
            var localFunction in CancellationTokenCallSites.CollectLocalFunctionsNeedingCancellationToken(
                semanticModel,
                method
            )
        )
        {
            replacements[localFunction] = ExtendLocalFunctionWithCancellationToken(semanticModel, localFunction);

            if (semanticModel.GetDeclaredSymbol(localFunction) is not IMethodSymbol localFunctionSymbol)
            {
                continue;
            }

            foreach (
                var invocation in CancellationTokenCallSites.CollectTopLevelInvocationsOf(
                    semanticModel,
                    method,
                    localFunctionSymbol
                )
            )
            {
                replacements[invocation] = AppendNamedArgument(invocation, ParameterName, LocalFunctionParameterName);
            }
        }

        return replacements.Count == 0
            ? method
            : method.ReplaceNodes(replacements.Keys, (originalNode, _) => replacements[originalNode]);
    }

    // A local function's new parameter is named "token", not "cancellationToken" — a local function can't
    // declare a parameter sharing a name with anything already in scope in its enclosing method (CS0136), and
    // the enclosing method either already has, or is about to gain, a "cancellationToken" of its own.
    private static LocalFunctionStatementSyntax ExtendLocalFunctionWithCancellationToken(
        SemanticModel semanticModel,
        LocalFunctionStatementSyntax localFunction
    )
    {
        var ownCandidates = CancellationTokenCallSites.CollectAppendableInvocations(semanticModel, localFunction);
        var propagated =
            ownCandidates.Count == 0
                ? localFunction
                : localFunction.ReplaceNodes(
                    ownCandidates.Select(candidate => candidate.Invocation),
                    (originalInvocation, _) =>
                        AppendNamedArgument(
                            originalInvocation,
                            LocalFunctionParameterName,
                            ownCandidates.First(candidate => candidate.Invocation == originalInvocation).ParameterName
                        )
                );

        var parameter = CreateCancellationTokenParameter(LocalFunctionParameterName);
        var updatedParameterList = InsertParameter(propagated.ParameterList, parameter);
        return propagated.WithParameterList(updatedParameterList);
    }

    private static InvocationExpressionSyntax AppendNamedArgument(
        InvocationExpressionSyntax invocation,
        string identifierName,
        string targetParameterName
    ) =>
        invocation.AddArgumentListArguments(
            SyntaxFactory
                .Argument(SyntaxFactory.IdentifierName(identifierName))
                .WithNameColon(SyntaxFactory.NameColon(targetParameterName))
        );

    // A params parameter must stay last in the parameter list, so the new CancellationToken parameter is
    // inserted right before it rather than appended after — appending after would produce invalid syntax.
    private static ParameterListSyntax InsertParameter(ParameterListSyntax parameterList, ParameterSyntax parameter)
    {
        var parameters = parameterList.Parameters;
        var paramsIndex = parameters.IndexOf(p => p.Modifiers.Any(SyntaxKind.ParamsKeyword));

        if (paramsIndex < 0)
        {
            return parameterList.AddParameters(parameter);
        }

        var updatedParameters = parameters.Insert(paramsIndex, parameter);
        return parameterList.WithParameters(updatedParameters);
    }

    private static CompilationUnitSyntax EnsureSystemThreadingUsing(CompilationUnitSyntax root)
    {
        var namespaceDeclaration = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();

        var hasUsing = root
            .Usings.Concat(namespaceDeclaration?.Usings ?? default)
            .Any(usingDirective =>
                usingDirective.Alias is null
                && string.Equals(
                    usingDirective.Name?.ToString(),
                    SystemThreadingNamespace,
                    System.StringComparison.Ordinal
                )
            );

        if (hasUsing)
        {
            return root;
        }

        // Match the line ending of an existing using directive, if there is one; otherwise fall back to a
        // bare line feed, matching this repository's LF convention (see .gitattributes).
        var existingUsing = root.Usings.Concat(namespaceDeclaration?.Usings ?? default).FirstOrDefault();
        var trailingTrivia =
            existingUsing is not null && existingUsing.GetTrailingTrivia() is { Count: > 0 } existingTrailing
                ? existingTrailing
                : SyntaxFactory.TriviaList(SyntaxFactory.LineFeed);

        var newUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(SystemThreadingNamespace))
            .WithTrailingTrivia(trailingTrivia);

        if (root.Usings.Count > 0)
        {
            return root.WithUsings(InsertAlphabetically(root.Usings, newUsing));
        }

        if (namespaceDeclaration is NamespaceDeclarationSyntax blockNamespace && blockNamespace.Usings.Count > 0)
        {
            var updatedNamespace = blockNamespace.WithUsings(InsertAlphabetically(blockNamespace.Usings, newUsing));
            return root.ReplaceNode(blockNamespace, updatedNamespace);
        }

        if (
            namespaceDeclaration is FileScopedNamespaceDeclarationSyntax fileScopedNamespace
            && fileScopedNamespace.Usings.Count > 0
        )
        {
            var updatedNamespace = fileScopedNamespace.WithUsings(
                InsertAlphabetically(fileScopedNamespace.Usings, newUsing)
            );
            return root.ReplaceNode(fileScopedNamespace, updatedNamespace);
        }

        return root.WithUsings(
            root.Usings.Add(
                newUsing.WithLeadingTrivia(root.Usings.Count == 0 ? default : root.Usings[0].GetLeadingTrivia())
            )
        );
    }

    private static SyntaxList<UsingDirectiveSyntax> InsertAlphabetically(
        SyntaxList<UsingDirectiveSyntax> usings,
        UsingDirectiveSyntax newUsing
    )
    {
        var newUsingName = newUsing.Name?.ToString() ?? string.Empty;

        for (var index = 0; index < usings.Count; index++)
        {
            var existingName = usings[index].Name?.ToString() ?? string.Empty;
            if (string.CompareOrdinal(newUsingName, existingName) < 0)
            {
                var displacedNode = usings[index];
                var displacedLeadingTrivia = displacedNode.GetLeadingTrivia();

                // The new directive takes over any leading blank line that separated the displaced node from
                // whatever preceded it (e.g. a namespace declaration); the displaced node, no longer first,
                // keeps only its indentation, not that blank line.
                var updatedUsings = usings.Replace(
                    displacedNode,
                    displacedNode.WithLeadingTrivia(RemoveLeadingBlankLines(displacedLeadingTrivia))
                );
                return updatedUsings.Insert(index, newUsing.WithLeadingTrivia(displacedLeadingTrivia));
            }
        }

        return usings.Add(newUsing.WithLeadingTrivia(usings.Count > 0 ? usings[0].GetLeadingTrivia() : default));
    }

    private static SyntaxTriviaList RemoveLeadingBlankLines(SyntaxTriviaList trivia)
    {
        var skip = 0;
        while (skip < trivia.Count && trivia[skip].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            skip++;
        }

        return SyntaxFactory.TriviaList(trivia.Skip(skip));
    }
}
