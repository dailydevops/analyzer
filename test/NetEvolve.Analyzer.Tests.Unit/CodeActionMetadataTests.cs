namespace NetEvolve.Analyzer.Tests.Unit;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using NetEvolve.Analyzer.Documentation;
using NetEvolve.Analyzer.Maintainability;
using NetEvolve.Analyzer.Naming;
using NetEvolve.Analyzer.Style;
using NetEvolve.Analyzer.Tests.Unit.Verifiers;
using NetEvolve.Analyzer.Usage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Pins the user-visible title and the Fix All equivalence key of every code action the code fixes register.
/// The code-fix verifiers only compare the fixed source, so a wrong or empty title (IDE lightbulb text) or
/// key (Fix All grouping) would otherwise go unnoticed.
/// </summary>
public sealed class CodeActionMetadataTests
{
    private const string GlobalConfig =
        "is_global = true\nbuild_property.RootNamespace = Geometry\nbuild_property.ProjectDir = /proj\n";

    [Test]
    public async Task NE0001_SingleType_RenameFile() =>
        await Assert
            .That(
                await CodeActionProbe<OneTypePerFileAnalyzer, OneTypePerFileCodeFixProvider>
                    .DescribeAsync("namespace Geometry;\n\npublic sealed class Circle { }\n", "NE0001", "Shapes.cs")
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Rename file to 'Circle.cs' [NE0001.RenameFile]");

    [Test]
    public async Task NE0001_MultipleTypes_MoveType() =>
        await Assert
            .That(
                await CodeActionProbe<OneTypePerFileAnalyzer, OneTypePerFileCodeFixProvider>
                    .DescribeAsync(
                        "namespace Geometry;\n\npublic sealed class Circle { }\n\npublic sealed class Square { }\n",
                        "NE0001",
                        "Circle.cs"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Move type to 'Square.cs' [NE0001.MoveType]");

    [Test]
    public async Task NE0002_ChangeNamespace() =>
        await Assert
            .That(
                await CodeActionProbe<NamespaceMatchesFolderAnalyzer, NamespaceMatchesFolderCodeFixProvider>
                    .DescribeAsync(
                        "namespace Wrong;\n\npublic sealed class Circle { }\n",
                        "NE0002",
                        "/proj/Shapes/Circle.cs",
                        globalConfig: GlobalConfig
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Change namespace to 'Geometry.Shapes' [NE0002.ChangeNamespace]");

    [Test]
    public async Task NE0003_Flatten() =>
        await Assert
            .That(
                await CodeActionProbe<SingleNamespacePerFileAnalyzer, SingleNamespacePerFileCodeFixProvider>
                    .DescribeAsync(
                        "namespace Outer\n{\n    namespace Inner\n    {\n        public sealed class Circle { }\n    }\n}\n",
                        "NE0003",
                        "Circle.cs"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Flatten to a single namespace [NE0003.Flatten]");

    [Test]
    public async Task NE0004_UseIsNull() =>
        await Assert
            .That(
                await CodeActionProbe<UseIsNullAnalyzer, UseIsNullCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample { public bool Check(object value) => value == null; }",
                        "NE0004"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Use 'is null' pattern [NE0004.UseIsNull]");

    [Test]
    [Arguments(LanguageVersion.Latest, "Use 'is not null' [NE0005.UseIsNotNull]")]
    [Arguments(LanguageVersion.CSharp8, "Use '!(... is null)' [NE0005.UseIsNotNull]")]
    public async Task NE0005_UseIsNotNull(LanguageVersion languageVersion, string expected) =>
        await Assert
            .That(
                await CodeActionProbe<UseIsNotNullAnalyzer, UseIsNotNullCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample { public bool Check(string value) => value != null; }",
                        "NE0005",
                        languageVersion: languageVersion
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo(expected);

    [Test]
    public async Task NE0006_UseIsNotNullOverIsObject() =>
        await Assert
            .That(
                await CodeActionProbe<UseIsNotNullOverIsObjectAnalyzer, UseIsNotNullOverIsObjectCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample { public bool Check(string value) => value is object; }",
                        "NE0006"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Use 'is not null' [NE0006.UseIsNotNull]");

    [Test]
    public async Task NE0007_UseLangword() =>
        await Assert
            .That(
                await CodeActionProbe<UseLangwordAnalyzer, UseLangwordCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample\n{\n    /// <summary>Returns <c>true</c> on success.</summary>\n    public bool Succeeded() => true;\n}\n",
                        "NE0007"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("""Use <see langword="true"/> instead [NE0007.UseLangword]""");

    [Test]
    public async Task NE0008_UseCref() =>
        await Assert
            .That(
                await CodeActionProbe<NativeTypeCrefAnalyzer, NativeTypeCrefCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample\n{\n    /// <summary>Returns a <c>string</c>.</summary>\n    public string Value() => \"\";\n}\n",
                        "NE0008"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("""Use <see cref="string"/> instead [NE0008.UseCref]""");

    [Test]
    public async Task NE0009_BothCheckForms() =>
        await Assert
            .That(
                await CodeActionProbe<RequireCancellationCheckAnalyzer, RequireCancellationCheckCodeFixProvider>
                    .DescribeAsync(
                        "using System.Threading;\n\npublic sealed class Sample\n{\n    public void Run(CancellationToken cancellationToken)\n    {\n        DoWork();\n    }\n\n    private static void DoWork() { }\n}\n",
                        "NE0009"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo(
                "Add ThrowIfCancellationRequested() check [NE0009.ThrowIfCancellationRequested]"
                    + " | Add IsCancellationRequested check [NE0009.IsCancellationRequested]"
            );

    [Test]
    public async Task NE0010_AddCancellationTokenParameter() =>
        await Assert
            .That(
                await CodeActionProbe<
                    RequireCancellationTokenParameterAnalyzer,
                    RequireCancellationTokenParameterCodeFixProvider
                >
                    .DescribeAsync(
                        "using System.Threading;\nusing System.Threading.Tasks;\n\npublic sealed class Sample\n{\n    public Task Run() => HelperAsync();\n\n    private static Task HelperAsync(CancellationToken token = default) => Task.CompletedTask;\n}\n",
                        "NE0010"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Add CancellationToken parameter [NE0010.RequireCancellationTokenParameter]");

    [Test]
    public async Task NE0011_RemoveRegion() =>
        await Assert
            .That(
                await CodeActionProbe<AvoidRegionDirectivesAnalyzer, AvoidRegionDirectivesCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample\n{\n    public void DoWork()\n    {\n        #region Logic\n        var x = 1;\n        #endregion\n    }\n}\n",
                        "NE0011"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Remove '#region' directive [NE0011.RemoveRegion]");

    [Test]
    public async Task NE0012_SingleSuffix() =>
        await Assert
            .That(
                await CodeActionProbe<RequireNumericLiteralSuffixAnalyzer, RequireNumericLiteralSuffixCodeFixProvider>
                    .DescribeAsync("public sealed class Sample { public long Value = 0; }", "NE0012")
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Use the 'L' suffix [NE0012.AddSuffix]");

    [Test]
    public async Task NE0012_AmbiguousOverloads_OneKeylessActionPerSuffix() =>
        await Assert
            .That(
                await CodeActionProbe<RequireNumericLiteralSuffixAnalyzer, RequireNumericLiteralSuffixCodeFixProvider>
                    .DescribeAsync(
                        "public sealed class Sample\n{\n    public void Accept(long number) { }\n\n    public void Accept(double number) { }\n\n    public void Call() => Accept(0);\n}\n",
                        "NE0012"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Use the 'L' suffix [-] | Use the 'D' suffix [-]");

    [Test]
    public async Task NE0014_RenameIdentifier() =>
        await Assert
            .That(
                await CodeActionProbe<AvoidInvisibleCharactersAnalyzer, AvoidInvisibleCharactersCodeFixProvider>
                    .DescribeAsync("public sealed class My\u200BClass { }", "NE0014")
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Rename to 'MyClass' [NE0014.RemoveInvisibleCharacters]");

    [Test]
    public async Task NE0014_RemoveTextSpan() =>
        await Assert
            .That(
                await CodeActionProbe<AvoidInvisibleCharactersAnalyzer, AvoidInvisibleCharactersCodeFixProvider>
                    .DescribeAsync("\uFEFFnamespace My.Project;\n\npublic sealed class Sample { }\n", "NE0014")
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Remove non-representable character [NE0014.RemoveInvisibleTextSpan]");

    [Test]
    public async Task NE0015_RemoveRedundantClear() =>
        await Assert
            .That(
                await CodeActionProbe<
                    AvoidRedundantCollectionClearAnalyzer,
                    AvoidRedundantCollectionClearCodeFixProvider
                >
                    .DescribeAsync(
                        "using System.Collections.Generic;\n\npublic sealed class Sample\n{\n    public void Run()\n    {\n        var list = new List<long>();\n        list.Clear();\n        list.Add(1L);\n    }\n}\n",
                        "NE0015"
                    )
                    .ConfigureAwait(false)
            )
            .IsEqualTo("Remove redundant 'Clear()' call [NE0015.RemoveRedundantClear]");
}
