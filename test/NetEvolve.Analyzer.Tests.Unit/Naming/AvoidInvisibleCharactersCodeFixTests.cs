namespace NetEvolve.Analyzer.Tests.Unit.Naming;

using System.Threading.Tasks;
using NetEvolve.Analyzer.Naming;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using TUnit.Core;

/// <summary>
/// Code-fix tests for NE0014: renaming a declaration with every Unicode "Format" category character
/// stripped out, with every reference to the symbol updated along with the declaration.
/// </summary>
public sealed class AvoidInvisibleCharactersCodeFixTests
{
    // A zero-width space (U+200B) — the same category of character removed in the motivating fix.
    private const string ZeroWidthSpace = "\u200B";

    // A byte-order mark / zero-width no-break space (U+FEFF).
    private const string Bom = "\uFEFF";

    [Test]
    public Task ClassName_RenamesDeclarationAndUsage() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class {|NE0014:My{{ZeroWidthSpace}}Class|}
            {
            }

            public sealed class Consumer
            {
                public My{{ZeroWidthSpace}}Class? Field;
            }
            """,
            """
            public sealed class MyClass
            {
            }

            public sealed class Consumer
            {
                public MyClass? Field;
            }
            """
        );

    [Test]
    public Task MethodName_RenamesDeclarationAndCallSite() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class Sample
            {
                public void {|NE0014:Do{{ZeroWidthSpace}}Work|}() { }

                public void Run() => Do{{ZeroWidthSpace}}Work();
            }
            """,
            """
            public sealed class Sample
            {
                public void DoWork() { }

                public void Run() => DoWork();
            }
            """
        );

    [Test]
    public Task ParameterName_RenamesDeclarationAndUsage() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class Sample
            {
                public void DoWork(int {|NE0014:va{{ZeroWidthSpace}}lue|})
                {
                    var x = va{{ZeroWidthSpace}}lue;
                }
            }
            """,
            """
            public sealed class Sample
            {
                public void DoWork(int value)
                {
                    var x = value;
                }
            }
            """
        );

    [Test]
    public Task LocalVariableName_RenamesDeclarationAndUsage() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class Sample
            {
                public void DoWork()
                {
                    var {|NE0014:va{{ZeroWidthSpace}}lue|} = 1;
                    _ = va{{ZeroWidthSpace}}lue;
                }
            }
            """,
            """
            public sealed class Sample
            {
                public void DoWork()
                {
                    var value = 1;
                    _ = value;
                }
            }
            """
        );

    [Test]
    public Task NamespaceSegment_RenamesDeclaration() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            namespace {|NE0014:My{{ZeroWidthSpace}}Project|}.Sub;

            public sealed class Sample
            {
            }
            """,
            """
            namespace MyProject.Sub;

            public sealed class Sample
            {
            }
            """
        );

    [Test]
    public Task StrayByteOrderMarkBeforeNamespaceKeyword_RemovesCharacter() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            {|NE0014:{{Bom}}|}namespace My.Project;

            public sealed class Sample
            {
            }
            """,
            """
            namespace My.Project;

            public sealed class Sample
            {
            }
            """
        );

    [Test]
    public Task TwoFlaggedDeclarationsInOneFile_BothRenamed() =>
        CSharpCodeFixVerifier<
            AvoidInvisibleCharactersAnalyzer,
            AvoidInvisibleCharactersCodeFixProvider
        >.VerifyCodeFixAsync(
            $$"""
            public sealed class {|NE0014:My{{ZeroWidthSpace}}Class|}
            {
                public void {|NE0014:Do{{ZeroWidthSpace}}Work|}() { }
            }
            """,
            """
            public sealed class MyClass
            {
                public void DoWork() { }
            }
            """
        );
}
