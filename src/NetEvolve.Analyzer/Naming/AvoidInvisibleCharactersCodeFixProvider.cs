namespace NetEvolve.Analyzer.Naming;

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using NetEvolve.Analyzer.Helpers;

/// <summary>
/// Code fix for <see cref="AvoidInvisibleCharactersAnalyzer">NE0014</see>. Offers one of two mechanical
/// fixes, matching which of the analyzer's two cases the diagnostic came from:
/// <list type="bullet">
/// <item>A type, member, parameter, type parameter, or local variable identifier: renames the declaration
/// to the same name with every Unicode "Format" category character stripped out, using <see
/// cref="Renamer"/> so every reference across the solution is updated along with the declaration.</item>
/// <item>A namespace segment, or stray whitespace/newline trivia: removes the offending character(s) as a
/// plain text edit local to this occurrence. A namespace segment is not renamed through <see
/// cref="Renamer"/> — asking the semantic model for the declared symbol of a multi-segment namespace
/// declaration resolves to the innermost merged namespace, not the specific outer segment the diagnostic
/// is about, so renaming that symbol would rename the wrong segment.</item>
/// </list>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidInvisibleCharactersCodeFixProvider))]
[Shared]
public sealed class AvoidInvisibleCharactersCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticIds.NE0014);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var root = (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;
        var start = diagnostic.Location.SourceSpan.Start;

        var trivia = root.FindTrivia(start, findInsideTrivia: true);
        if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
        {
            RegisterTextEditFix(context, diagnostic, trivia.Span);
            return;
        }

        var token = root.FindToken(start);
        if (IsNamespaceNameToken(token))
        {
            RegisterTextEditFix(context, diagnostic, token.Span);
            return;
        }

        RegisterIdentifierFix(context, diagnostic, token);
    }

    /// <summary>
    /// True when <paramref name="token"/> is one of the identifier segments making up a <see
    /// langword="namespace"/> declaration's name (as opposed to, say, a type identifier — which happens to
    /// also sit under a <see cref="BaseNamespaceDeclarationSyntax"/> ancestor, but outside its <c>Name</c>).
    /// </summary>
    private static bool IsNamespaceNameToken(SyntaxToken token) =>
        token.Parent?.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>() is { } namespaceDeclaration
        && namespaceDeclaration.Name.Span.Contains(token.Span);

    private static void RegisterIdentifierFix(CodeFixContext context, Diagnostic diagnostic, SyntaxToken token)
    {
        // ValueText already has every formatting character removed per the C# specification — exactly the
        // rename target — while Text (the raw source spelling) is what the analyzer flagged.
        var cleanName = token.ValueText;
        if (cleanName.Length == 0)
        {
            // Nothing would be left to rename to; leave the diagnostic for a manual rename.
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Rename to '{cleanName}'",
                cancellationToken => RenameAsync(context.Document, token, cleanName, cancellationToken),
                equivalenceKey: "NE0014.RemoveInvisibleCharacters"
            ),
            diagnostic
        );
    }

    private static void RegisterTextEditFix(CodeFixContext context, Diagnostic diagnostic, TextSpan span) =>
        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove non-representable character",
                cancellationToken => RemoveCharactersAsync(context.Document, span, cancellationToken),
                equivalenceKey: "NE0014.RemoveInvisibleTextSpan"
            ),
            diagnostic
        );

    private static async Task<Document> RemoveCharactersAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var cleaned = InvisibleCharacters.Strip(text.ToString(span));

        return document.WithText(text.WithChanges(new TextChange(span, cleaned)));
    }

    private static async Task<Solution> RenameAsync(
        Document document,
        SyntaxToken token,
        string cleanName,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var semanticModel = (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false))!;
        var symbol = DeclaredSymbol(semanticModel, token, cancellationToken);
        if (symbol is null)
        {
            return document.Project.Solution;
        }

        return await Renamer
            .RenameSymbolAsync(
                document.Project.Solution,
                symbol,
                new SymbolRenameOptions(),
                cleanName,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Walks up from <paramref name="token"/>'s parent, asking the semantic model for a declared symbol at
    /// each ancestor, until one is found or a member declaration boundary is passed without a hit. This
    /// single walk resolves every declaration shape <see cref="RegisterIdentifierFix"/> is invoked for
    /// (type, member, parameter, type parameter, or local variable) without re-deriving the analyzer's
    /// dispatch logic here. Namespace segments never reach this method — see <see
    /// cref="IsNamespaceNameToken"/>.
    /// </summary>
    private static ISymbol? DeclaredSymbol(
        SemanticModel semanticModel,
        SyntaxToken token,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
            if (symbol is not null)
            {
                return symbol;
            }

            if (node is MemberDeclarationSyntax)
            {
                break;
            }
        }

        return null;
    }
}
