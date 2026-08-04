namespace NetEvolve.Analyzer.Usage;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// NE0009 — reports a method with a block body that accepts a <see cref="System.Threading.CancellationToken"/>
/// parameter but does not check for cancellation as the first statement of its body (immediately after any
/// leading argument-validation guard clauses). Either <c>token.ThrowIfCancellationRequested()</c> or
/// <c>if (token.IsCancellationRequested) { return ...; }</c> satisfies the rule. Expression-bodied methods are
/// skipped, since they cannot structurally hold a guard statement; methods without a body (interface members,
/// abstract, extern, or partial declarations without an implementation) are skipped as well, since there is no
/// body to check. Only <see cref="MethodDeclarationSyntax"/> is inspected; local functions are not.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequireCancellationCheckAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RequireCancellationCheck);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Expression-bodied members ('=>') cannot structurally hold a guard statement; methods without any
        // body (interface members, abstract/extern/partial declarations without an implementation) have
        // nothing to check either.
        if (method.Body is null)
        {
            return;
        }

        var tokenNames = GetCancellationTokenParameterNames(context.SemanticModel, method);
        if (tokenNames.IsEmpty)
        {
            return;
        }

        if (HasLeadingCancellationCheck(context.SemanticModel, method.Body.Statements, tokenNames))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.RequireCancellationCheck,
                method.Identifier.GetLocation(),
                method.Identifier.ValueText
            )
        );
    }

    /// <summary>The names of the method's parameters typed as <c>System.Threading.CancellationToken</c>.</summary>
    private static ImmutableArray<string> GetCancellationTokenParameterNames(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method
    )
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Type is null)
            {
                continue;
            }

            var type = semanticModel.GetTypeInfo(parameter.Type).Type;
            if (
                type is { Name: "CancellationToken" }
                && string.Equals(
                    type.ContainingNamespace?.ToDisplayString(),
                    "System.Threading",
                    StringComparison.Ordinal
                )
            )
            {
                builder.Add(parameter.Identifier.ValueText);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Walks the leading argument-validation guard clauses and checks whether the first non-guard statement is
    /// a cancellation check against one of <paramref name="tokenNames"/>.
    /// </summary>
    private static bool HasLeadingCancellationCheck(
        SemanticModel semanticModel,
        SyntaxList<StatementSyntax> statements,
        ImmutableArray<string> tokenNames
    )
    {
        foreach (var statement in statements)
        {
            if (IsGuardClause(semanticModel, statement))
            {
                continue;
            }

            return IsThrowIfCancellationRequested(statement, tokenNames)
                || IsIsCancellationRequestedReturn(statement, tokenNames);
        }

        // Every statement was a guard clause (or the body is empty): there is no statement left to hold the
        // cancellation check.
        return false;
    }

    /// <summary>
    /// Whether <paramref name="statement"/> is a leading argument-validation guard clause: either an
    /// <c>if</c>-statement whose sole statement throws an exception that is or derives from
    /// <see cref="System.ArgumentException"/>, or a call to a static <c>ThrowIfXxx</c> method on a type whose
    /// name ends with <c>Exception</c> (e.g. <c>ArgumentNullException.ThrowIfNull(...)</c>).
    /// </summary>
    private static bool IsGuardClause(SemanticModel semanticModel, StatementSyntax statement)
    {
        if (statement is IfStatementSyntax { Else: null } ifStatement)
        {
            return IsSingleArgumentExceptionThrow(semanticModel, ifStatement.Statement);
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

    // A guard's 'if' body throws exactly one exception, whether braced ("if (x is null) { throw ...; }") or not
    // ("if (x is null) throw ...;"); an else branch or any additional logic disqualifies the statement as a
    // guard clause (handled by the caller for 'else', here for extra statements inside a block).
    private static bool IsSingleArgumentExceptionThrow(SemanticModel semanticModel, StatementSyntax ifBody)
    {
        var throwStatement = ifBody switch
        {
            ThrowStatementSyntax single => single,
            BlockSyntax { Statements.Count: 1 } block when block.Statements[0] is ThrowStatementSyntax onlyStatement =>
                onlyStatement,
            _ => null,
        };

        if (throwStatement?.Expression is not ObjectCreationExpressionSyntax objectCreation)
        {
            return false;
        }

        var thrownType = semanticModel.GetTypeInfo(objectCreation).Type;
        var argumentExceptionType = semanticModel.Compilation.GetTypeByMetadataName("System.ArgumentException");

        if (argumentExceptionType is null)
        {
            return false;
        }

        for (var current = thrownType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, argumentExceptionType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsThrowIfCancellationRequested(StatementSyntax statement, ImmutableArray<string> tokenNames) =>
        statement
            is ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax tokenIdentifier,
                        Name.Identifier.ValueText: "ThrowIfCancellationRequested",
                    },
                    ArgumentList.Arguments.Count: 0,
                },
            }
        && tokenNames.Contains(tokenIdentifier.Identifier.ValueText, StringComparer.Ordinal);

    private static bool IsIsCancellationRequestedReturn(StatementSyntax statement, ImmutableArray<string> tokenNames)
    {
        if (
            statement
            is not IfStatementSyntax
            {
                Else: null,
                Condition: MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax tokenIdentifier,
                    Name.Identifier.ValueText: "IsCancellationRequested",
                },
            } ifStatement
        )
        {
            return false;
        }

        if (!tokenNames.Contains(tokenIdentifier.Identifier.ValueText, StringComparer.Ordinal))
        {
            return false;
        }

        return ifStatement.Statement switch
        {
            ReturnStatementSyntax => true,
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0] is ReturnStatementSyntax,
            _ => false,
        };
    }
}
