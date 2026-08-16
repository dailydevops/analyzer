namespace NetEvolve.Analyzer.Usage;

using System;
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
/// Code fix for <see cref="RequireCancellationCheckAnalyzer">NE0009</see>. Applies equally to a method or a
/// local function. Offers two independent fixes at the position immediately after the leading guard clauses:
/// inserting
/// <c>token.ThrowIfCancellationRequested();</c>, or inserting
/// <c>if (token.IsCancellationRequested) { return ...; }</c>. Both are always registered, so the user (or Fix
/// All) can choose either form for a given occurrence. The inserted text is built directly (rather than via
/// <see cref="Microsoft.CodeAnalysis.Formatting.Formatter"/>) so the result always uses the same line-feed-only
/// endings as the rest of the document, independent of the host OS or workspace formatting options.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequireCancellationCheckCodeFixProvider))]
[Shared]
public sealed class RequireCancellationCheckCodeFixProvider : CodeFixProvider
{
    private const string ThrowIfCancellationRequestedKey = "NE0009.ThrowIfCancellationRequested";
    private const string IsCancellationRequestedKey = "NE0009.IsCancellationRequested";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0009);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var diagnostic = context.Diagnostics[0];
        var declaration = FindMemberDeclaration(root.FindNode(diagnostic.Location.SourceSpan));

        var tokenName = GetCancellationTokenParameterName(GetDeclarationParts(declaration).ParameterList);
        if (tokenName is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add ThrowIfCancellationRequested() check",
                cancellationToken =>
                    InsertCheckAsync(
                        context.Document,
                        declaration,
                        tokenName,
                        BuildThrowIfCancellationRequestedTextAsync,
                        cancellationToken
                    ),
                equivalenceKey: ThrowIfCancellationRequestedKey
            ),
            diagnostic
        );

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add IsCancellationRequested check",
                cancellationToken =>
                    InsertCheckAsync(
                        context.Document,
                        declaration,
                        tokenName,
                        BuildIsCancellationRequestedTextAsync,
                        cancellationToken
                    ),
                equivalenceKey: IsCancellationRequestedKey
            ),
            diagnostic
        );
    }

    // The nearest method or local function at or above 'node' — the only two shapes NE0009 reports on. A
    // single walk checking both types per candidate: a local function nested inside a method must match
    // itself first, before its enclosing method further up the same walk.
    private static SyntaxNode FindMemberDeclaration(SyntaxNode node) =>
        node.AncestorsAndSelf()
            .First(candidate => candidate is MethodDeclarationSyntax or LocalFunctionStatementSyntax);

    // Pulls the four members shared by a method and a local function out of whichever shape 'declaration' is.
    // Every caller passes a node found by FindMemberDeclaration, so it is always one of these two shapes — no
    // fallback branch to leave uncovered.
    private static (
        BlockSyntax? Body,
        ParameterListSyntax ParameterList,
        TypeSyntax ReturnType,
        SyntaxTokenList Modifiers
    ) GetDeclarationParts(SyntaxNode declaration)
    {
        if (declaration is MethodDeclarationSyntax method)
        {
            return (method.Body, method.ParameterList, method.ReturnType, method.Modifiers);
        }

        var localFunction = (LocalFunctionStatementSyntax)declaration;
        return (localFunction.Body, localFunction.ParameterList, localFunction.ReturnType, localFunction.Modifiers);
    }

    private static async Task<Document> InsertCheckAsync(
        Document document,
        SyntaxNode declaration,
        string tokenName,
        Func<Document, SyntaxNode, string, string, CancellationToken, Task<string>> buildStatementText,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
        var current = FindMemberDeclaration(root.FindNode(declaration.Span));

        var body = GetDeclarationParts(current).Body!;
        var statements = body.Statements;
        var guardCount = CountLeadingGuardClauses(statements);
        var indentation = GetIndentation(current, statements, guardCount);

        var statementText = await buildStatementText(document, current, tokenName, indentation, cancellationToken)
            .ConfigureAwait(false);

        // Separate the inserted check from the guard clauses above it with exactly one blank line, matching the
        // blank line normalized below between the check and whatever follows it. Only applies when there's a
        // preceding guard clause; the check being the method's first statement needs no leading gap.
        var checkLeadingTrivia =
            guardCount > 0
                ? SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace(indentation))
                : SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(indentation));
        var checkStatement = SyntaxFactory.ParseStatement(statementText).WithLeadingTrivia(checkLeadingTrivia);

        var newStatements = statements.Insert(guardCount, checkStatement);

        // Normalize the gap between the inserted check and whatever follows it to exactly one blank line: none
        // was there before (the check would otherwise butt up directly against it), and if the source already
        // had extra blank lines there, this collapses them back down to one. Only the leading run of pure
        // blank-line filler (end-of-line and whitespace) is replaced; anything after that run — a comment, a
        // directive, whatever — is kept as-is, so a leading "// Arrange"-style comment still gets its blank
        // line above it without losing the comment itself.
        if (guardCount < statements.Count)
        {
            var followingStatement = newStatements[guardCount + 1];
            var leadingTrivia = followingStatement.GetLeadingTrivia();
            var blankRunEnd = 0;
            while (
                blankRunEnd < leadingTrivia.Count
                && (
                    leadingTrivia[blankRunEnd].IsKind(SyntaxKind.EndOfLineTrivia)
                    || leadingTrivia[blankRunEnd].IsKind(SyntaxKind.WhitespaceTrivia)
                )
            )
            {
                blankRunEnd++;
            }

            var normalizedFollowing = followingStatement.WithLeadingTrivia(
                SyntaxFactory
                    .TriviaList(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace(indentation))
                    .AddRange(leadingTrivia.Skip(blankRunEnd))
            );
            newStatements = newStatements.Replace(followingStatement, normalizedFollowing);
        }

        var newBody = body.WithStatements(newStatements);

        return document.WithSyntaxRoot(root.ReplaceNode(body, newBody));
    }

    // The indentation (as plain whitespace text) that the inserted statement should use: that of the statement
    // it will be inserted before, or of the last statement if inserted at the end, or one level deeper than
    // the declaration itself if the body is empty.
    private static string GetIndentation(
        SyntaxNode declaration,
        SyntaxList<StatementSyntax> statements,
        int insertionIndex
    )
    {
        if (insertionIndex < statements.Count)
        {
            return GetTrailingIndentation(statements[insertionIndex].GetLeadingTrivia());
        }

        if (statements.Count > 0)
        {
            return GetTrailingIndentation(statements[statements.Count - 1].GetLeadingTrivia());
        }

        return GetTrailingIndentation(declaration.GetLeadingTrivia()) + "    ";
    }

    // A statement's (or the method's) leading trivia isn't just its indentation: it's everything back to the
    // previous token, which can include blank lines, comments, or (for a doc comment) a trivia node whose own
    // text ends with a newline that isn't itself a separate EndOfLineTrivia. Using the raw trivia text as
    // "indentation" bakes those extra lines into every spot the caller inserts it (the check's own leading
    // trivia, and each line of the generated if-block), turning a single blank line or comment before the
    // insertion point into a duplicated line after every generated line. What's actually wanted is only the
    // contiguous run of whitespace immediately preceding the token - i.e. trailing whitespace trivia, walking
    // backwards from the end of the list until something else (end-of-line, comment, ...) is hit.
    private static string GetTrailingIndentation(SyntaxTriviaList leadingTrivia)
    {
        var start = leadingTrivia.Count;

        while (start > 0 && leadingTrivia[start - 1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            start--;
        }

        return string.Concat(leadingTrivia.Skip(start).Select(trivia => trivia.ToFullString()));
    }

    // Mirrors RequireCancellationCheckAnalyzer.IsGuardClause using only syntax (no semantic model needed here:
    // the diagnostic already established the exact guard-clause run for this method).
    private static int CountLeadingGuardClauses(SyntaxList<StatementSyntax> statements)
    {
        var count = 0;

        foreach (var statement in statements)
        {
            if (!IsGuardClauseLike(statement))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static bool IsGuardClauseLike(StatementSyntax statement)
    {
        if (statement is IfStatementSyntax { Else: null } ifStatement)
        {
            var body = ifStatement.Statement switch
            {
                ThrowStatementSyntax single => single,
                BlockSyntax { Statements.Count: 1 } block
                    when block.Statements[0] is ThrowStatementSyntax onlyStatement => onlyStatement,
                _ => null,
            };

            if (body?.Expression is ObjectCreationExpressionSyntax { Type: SimpleNameSyntax typeSyntax })
            {
                return typeSyntax.Identifier.ValueText.EndsWith("Exception", StringComparison.Ordinal);
            }

            return false;
        }

        if (
            statement is ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax typeName } memberAccess,
                },
            }
        )
        {
            var methodName = memberAccess.Name.Identifier.ValueText;
            return methodName.StartsWith("ThrowIf", StringComparison.Ordinal)
                && typeName.Identifier.ValueText.EndsWith("Exception", StringComparison.Ordinal);
        }

        return false;
    }

    private static string? GetCancellationTokenParameterName(ParameterListSyntax parameterList) =>
        parameterList
            .Parameters.FirstOrDefault(parameter =>
                parameter.Type
                    is IdentifierNameSyntax { Identifier.ValueText: "CancellationToken" }
                        or QualifiedNameSyntax { Right.Identifier.ValueText: "CancellationToken" }
            )
            ?.Identifier.ValueText;

    private static Task<string> BuildThrowIfCancellationRequestedTextAsync(
        Document _,
        SyntaxNode _1,
        string tokenName,
        string _2,
        CancellationToken _3
    ) => Task.FromResult($"{tokenName}.ThrowIfCancellationRequested();\n");

    private static async Task<string> BuildIsCancellationRequestedTextAsync(
        Document document,
        SyntaxNode declaration,
        string tokenName,
        string indentation,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var returnText = await GetReturnStatementTextAsync(document, declaration, cancellationToken)
            .ConfigureAwait(false);
        return $"if ({tokenName}.IsCancellationRequested)\n{indentation}{{\n{indentation}    {returnText}\n{indentation}}}\n";
    }

    // The well-known generic collection shapes a C# 12 collection expression ('[]') can legally target: BCL
    // list/set/queue/stack interfaces and implementations, their System.Collections.Immutable counterparts, and
    // Span<T>/ReadOnlySpan<T>. Array types are handled separately (any element type, any rank). This is a
    // pragmatic subset, not the full collection-expression conversion spec (custom [CollectionBuilder] types are
    // not recognized), but covers what a guard-clause return is realistically going to need.
    private static readonly ImmutableHashSet<(string Namespace, string Name)> CollectionExpressionTargetTypes =
        ImmutableHashSet.Create(
            ("System.Collections.Generic", "List"),
            ("System.Collections.Generic", "IList"),
            ("System.Collections.Generic", "ICollection"),
            ("System.Collections.Generic", "IEnumerable"),
            ("System.Collections.Generic", "IReadOnlyList"),
            ("System.Collections.Generic", "IReadOnlyCollection"),
            ("System.Collections.Generic", "ISet"),
            ("System.Collections.Generic", "HashSet"),
            ("System.Collections.Generic", "SortedSet"),
            ("System.Collections.Generic", "Stack"),
            ("System.Collections.Generic", "Queue"),
            ("System.Collections.Immutable", "ImmutableArray"),
            ("System.Collections.Immutable", "ImmutableList"),
            ("System.Collections.Immutable", "IImmutableList"),
            ("System.Collections.Immutable", "ImmutableHashSet"),
            ("System.Collections.Immutable", "IImmutableSet"),
            ("System.Collections.Immutable", "ImmutableSortedSet"),
            ("System.Collections.Immutable", "ImmutableQueue"),
            ("System.Collections.Immutable", "IImmutableQueue"),
            ("System.Collections.Immutable", "ImmutableStack"),
            ("System.Collections.Immutable", "IImmutableStack"),
            ("System", "Span"),
            ("System", "ReadOnlySpan")
        );

    // 'yield break;' is required (and the only legal early-exit) in an iterator method — 'return value;' does
    // not compile there at all, regardless of return type. 'return;' is legal for a void-returning method, and
    // also for an async method returning (non-generic) Task or ValueTask. For a real T, an async method
    // returning Task<T>/ValueTask<T>, a plain synchronous collection-returning method, or a non-async
    // Task<T>/ValueTask<T> (where the statement's target type is Task<T> itself, not T) a collection-expression
    // 'return [];' is used when the target type is one of the well-known collection shapes above and the
    // project's language version supports collection expressions (C# 12); otherwise 'return default;' is the
    // universal fallback — always syntactically legal, even where it is only a semantic placeholder.
    private static async Task<string> GetReturnStatementTextAsync(
        Document document,
        SyntaxNode declaration,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (body, _, returnType, modifiers) = GetDeclarationParts(declaration);

        if (IsIteratorMethod(body!))
        {
            return "yield break;";
        }

        if (returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return "return;";
        }

        var isAsync = modifiers.Any(SyntaxKind.AsyncKeyword);
        if (isAsync && returnType is IdentifierNameSyntax { Identifier.ValueText: "Task" or "ValueTask" })
        {
            return "return;";
        }

        // LanguageVersion.CSharp12 doesn't exist on the older Microsoft.CodeAnalysis.CSharp package versions this
        // analyzer also builds against (Roslyn 4.4/4.7 predate C# 12); the enum's underlying values are stable
        // API surface across versions, so referencing it by value keeps this compiling on all four variants.
        if (LanguageVersionGate.Supports(document, (LanguageVersion)1200))
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var methodSymbol = semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) as IMethodSymbol;

            if (
                methodSymbol is not null
                && IsCollectionExpressionCompatible(GetReturnTargetType(methodSymbol, isAsync))
            )
            {
                return "return [];";
            }
        }

        return "return default;";
    }

    // The type a 'return X;' statement's X must have: for an async method returning Task<T>/ValueTask<T>, the
    // compiler unwraps to T; everywhere else (including a non-async Task<T>/ValueTask<T>, where X must itself
    // be a Task<T>/ValueTask<T>) the declared return type is exactly what X must be.
    private static ITypeSymbol GetReturnTargetType(IMethodSymbol methodSymbol, bool isAsync)
    {
        if (
            isAsync
            && methodSymbol.ReturnType
                is INamedTypeSymbol { Name: "Task" or "ValueTask", TypeArguments.Length: 1 } taskType
        )
        {
            return taskType.TypeArguments[0];
        }

        return methodSymbol.ReturnType;
    }

    private static bool IsCollectionExpressionCompatible(ITypeSymbol? type) =>
        type switch
        {
            IArrayTypeSymbol => true,
            INamedTypeSymbol { Arity: 1 } named => CollectionExpressionTargetTypes.Contains(
                (named.ContainingNamespace.ToDisplayString(), named.Name)
            ),
            _ => false,
        };

    // Whether 'body' is (or contains, at its own nesting level) an iterator — i.e. already uses 'yield
    // return'/'yield break' somewhere in its body. Descent stops at a nested local function, since a local
    // function's own 'yield' makes only that local function an iterator, not the enclosing member.
    private static bool IsIteratorMethod(BlockSyntax body) =>
        body.DescendantNodes(descendIntoChildren: node => node is not LocalFunctionStatementSyntax)
            .OfType<YieldStatementSyntax>()
            .Any();
}
