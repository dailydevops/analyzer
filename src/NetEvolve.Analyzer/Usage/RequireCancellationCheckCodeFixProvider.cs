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

/// <summary>
/// Code fix for <see cref="RequireCancellationCheckAnalyzer">NE0009</see>. Offers two independent fixes at the
/// position immediately after the method's leading argument-validation guard clauses: inserting
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
        var method = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();

        var tokenName = GetCancellationTokenParameterName(method);
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
                        method,
                        tokenName,
                        BuildThrowIfCancellationRequestedText,
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
                        method,
                        tokenName,
                        BuildIsCancellationRequestedText,
                        cancellationToken
                    ),
                equivalenceKey: IsCancellationRequestedKey
            ),
            diagnostic
        );
    }

    private static async Task<Document> InsertCheckAsync(
        Document document,
        MethodDeclarationSyntax method,
        string tokenName,
        Func<MethodDeclarationSyntax, string, string, string> buildStatementText,
        CancellationToken cancellationToken
    )
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
        var current = root.FindNode(method.Span).AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

        var body = current.Body!;
        var statements = body.Statements;
        var guardCount = CountLeadingGuardClauses(statements);
        var indentation = GetIndentation(current, statements, guardCount);

        var statementText = buildStatementText(current, tokenName, indentation);
        var checkStatement = SyntaxFactory
            .ParseStatement(statementText)
            .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation));

        var newStatements = statements.Insert(guardCount, checkStatement);
        var newBody = body.WithStatements(newStatements);

        return document.WithSyntaxRoot(root.ReplaceNode(body, newBody));
    }

    // The indentation (as plain whitespace text) that the inserted statement should use: that of the statement
    // it will be inserted before, or of the last statement if inserted at the end, or one level deeper than
    // the method itself if the body is empty.
    private static string GetIndentation(
        MethodDeclarationSyntax method,
        SyntaxList<StatementSyntax> statements,
        int insertionIndex
    )
    {
        if (insertionIndex < statements.Count)
        {
            return statements[insertionIndex].GetLeadingTrivia().ToFullString();
        }

        if (statements.Count > 0)
        {
            return statements[statements.Count - 1].GetLeadingTrivia().ToFullString();
        }

        return method.GetLeadingTrivia().ToFullString() + "    ";
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

    private static string? GetCancellationTokenParameterName(MethodDeclarationSyntax method) =>
        method
            .ParameterList.Parameters.FirstOrDefault(parameter =>
                parameter.Type
                    is IdentifierNameSyntax { Identifier.ValueText: "CancellationToken" }
                        or QualifiedNameSyntax { Right.Identifier.ValueText: "CancellationToken" }
            )
            ?.Identifier.ValueText;

    private static string BuildThrowIfCancellationRequestedText(
        MethodDeclarationSyntax _,
        string tokenName,
        string _1
    ) => $"{tokenName}.ThrowIfCancellationRequested();\n";

    private static string BuildIsCancellationRequestedText(
        MethodDeclarationSyntax method,
        string tokenName,
        string indentation
    )
    {
        var returnText = GetReturnStatementText(method);
        return $"if ({tokenName}.IsCancellationRequested)\n{indentation}{{\n{indentation}    {returnText}\n{indentation}}}\n";
    }

    // 'return;' is legal for a void-returning method, and also for an async method returning (non-generic)
    // Task or ValueTask; everywhere else (a real T, or a non-async Task<T>/ValueTask<T>/T) 'return default;' is
    // always syntactically legal, even where it is only a semantic placeholder.
    private static string GetReturnStatementText(MethodDeclarationSyntax method)
    {
        if (method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return "return;";
        }

        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        if (isAsync && method.ReturnType is IdentifierNameSyntax { Identifier.ValueText: "Task" or "ValueTask" })
        {
            return "return;";
        }

        return "return default;";
    }
}
