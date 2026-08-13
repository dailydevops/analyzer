namespace NetEvolve.Analyzer.Naming;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// NE0014 — flags a Unicode "Format" category character (for example a zero-width space/joiner, a
/// byte-order mark, or a bidirectional text-direction override) in either of two places:
/// <list type="bullet">
/// <item>Inside a declared identifier — a type, member, parameter, type parameter, local variable, or
/// namespace segment. Such characters are legal identifier-part characters per the C# language
/// specification, so the identifier still compiles and can even look identical to another declaration that
/// lacks the hidden character. Only the declaration is reported, not every reference to it — the
/// accompanying code fix renames the underlying symbol, updating every reference at once.</item>
/// <item>In the plain whitespace immediately before a token — in practice this only ever fires for a
/// byte-order mark (<c>U+FEFF</c>) that arrives as literal text content (for example pasted mid-file), not
/// as a genuine file-level encoding BOM: the C# lexer special-cases a literal <c>U+FEFF</c> as whitespace
/// wherever it sits, while every other "Format" category character causes a hard compiler error
/// (<c>CS1056</c>) in that same free-standing position, so only a literal byte-order mark risks being
/// silently swallowed this way. A genuine encoding BOM at the very start of a UTF-8 file — the actual
/// three-byte <c>EF BB BF</c> sequence a text editor writes — is consumed by <see
/// cref="Microsoft.CodeAnalysis.Text.SourceText"/>'s encoding detection before the compiler ever tokenizes
/// the file, so it never becomes text and this rule structurally cannot see it; that is an editor/encoding
/// concern, not something any Roslyn analyzer can flag. The code fix removes the literal character as a
/// plain text edit.</item>
/// </list>
/// Either way the character renders as invisible or inconsistently across editors, terminals, and diff
/// tools. Comments, string literals, and character literals are left alone — free-form text may legitimately
/// contain these characters.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidInvisibleCharactersAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<SyntaxKind> DeclarationKinds = ImmutableArray.Create(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.LocalFunctionStatement,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.EnumMemberDeclaration,
        SyntaxKind.VariableDeclarator,
        SyntaxKind.Parameter,
        SyntaxKind.TypeParameter,
        SyntaxKind.CatchDeclaration,
        SyntaxKind.SingleVariableDesignation,
        SyntaxKind.ForEachStatement,
        SyntaxKind.NamespaceDeclaration,
        SyntaxKind.FileScopedNamespaceDeclaration
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.AvoidInvisibleCharacters);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeNode, DeclarationKinds);
        context.RegisterSyntaxTreeAction(AnalyzeTrivia);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        foreach (var token in NameTokens(context.Node))
        {
            // The token's resolved name already has formatting characters removed per the C# specification,
            // so detection has to run against its raw source spelling instead, where they still show up.
            if (!InvisibleCharacters.TryFind(token.Text, out var codePoints))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.AvoidInvisibleCharacters,
                    token.GetLocation(),
                    InvisibleCharacters.Format(codePoints),
                    $"in identifier '{token.ValueText}'"
                )
            );
        }
    }

    private static void AnalyzeTrivia(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            // Only plain whitespace trivia is inspected — comments, directives, and string/char literal
            // token text are free-form content where these characters may legitimately appear, and newline
            // trivia is exactly the line-break sequence, which never contains a "Format" character.
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                continue;
            }

            if (!InvisibleCharacters.TryFind(trivia.ToString(), out var codePoints))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.AvoidInvisibleCharacters,
                    trivia.GetLocation(),
                    InvisibleCharacters.Format(codePoints),
                    $"before '{trivia.Token.Text}'"
                )
            );
        }
    }

    /// <summary>
    /// Returns the identifier token(s) that make up the declared name of <paramref name="node"/> — every
    /// segment for a namespace declaration, a single token for everything else.
    /// </summary>
    private static IEnumerable<SyntaxToken> NameTokens(SyntaxNode node) =>
        node switch
        {
            BaseNamespaceDeclarationSyntax @namespace => @namespace
                .Name.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken)),
            BaseTypeDeclarationSyntax type => One(type.Identifier),
            DelegateDeclarationSyntax @delegate => One(@delegate.Identifier),
            MethodDeclarationSyntax method => One(method.Identifier),
            LocalFunctionStatementSyntax localFunction => One(localFunction.Identifier),
            PropertyDeclarationSyntax property => One(property.Identifier),
            EventDeclarationSyntax @event => One(@event.Identifier),
            EnumMemberDeclarationSyntax enumMember => One(enumMember.Identifier),
            VariableDeclaratorSyntax variable => One(variable.Identifier),
            ParameterSyntax parameter => One(parameter.Identifier),
            TypeParameterSyntax typeParameter => One(typeParameter.Identifier),
            CatchDeclarationSyntax catchDeclaration => One(catchDeclaration.Identifier),
            SingleVariableDesignationSyntax designation => One(designation.Identifier),
            ForEachStatementSyntax forEach => One(forEach.Identifier),
            _ => Array.Empty<SyntaxToken>(),
        };

    private static SyntaxToken[] One(SyntaxToken token) => new[] { token };
}
