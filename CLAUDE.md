# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`NetEvolve.Analyzer` is a Roslyn analyzer + code-fix NuGet package. The analyzer assembly targets
`netstandard2.0` (so it loads in every Roslyn host) and ships as a development dependency only —
`src/NetEvolve.Analyzer/NetEvolve.Analyzer.csproj` builds with `IncludeBuildOutput=false` and packs the
assembly directly under `analyzers/dotnet/cs` via the `_PackAnalyzer` target, no `lib/` folder, no
runtime deps. Rules are grouped by category folder (`Maintainability/`, `Usage/`, plus empty
placeholder folders for categories not yet used: `Design`, `Documentation`, `Globalization`,
`Interoperability`, `Naming`, `Performance`, `Reliability`, `Security`, `Style`).

Solution layout: `Analyzer.slnx` → `src/NetEvolve.Analyzer` (the package) and two test projects under
`test/`: `NetEvolve.Analyzer.Tests.Unit` and `NetEvolve.Analyzer.Tests.Integration`.

## Commands

```bash
dotnet restore
dotnet build
dotnet test                                          # runs both test projects
dotnet test test/NetEvolve.Analyzer.Tests.Unit
dotnet test test/NetEvolve.Analyzer.Tests.Integration
csharpier format .                                   # required formatting; CI enforces it
```

Test projects use **TUnit** on **Microsoft.Testing.Platform** (`OutputType=Exe`, multi-targeted
`net8.0;net9.0;net10.0`), not xUnit/MSTest/VSTest. `dotnet test` works, but to filter to a single test
run the test executable directly with the platform's own filter syntax rather than `dotnet test
--filter`:

```bash
dotnet run --project test/NetEvolve.Analyzer.Tests.Unit -- --treenode-filter "/*/*/OneTypePerFileAnalyzerTests/*"
```

Mutation testing is configured via `stryker-config.json` (Stryker.NET, `mutation-level: Complete`,
break threshold 0) covering `src/NetEvolve.Analyzer/NetEvolve.Analyzer.csproj` against both test
projects — run with the `dotnet-stryker` tool if verifying test strength.

CI (`.github/workflows/cicd.yml`) delegates to a shared reusable workflow
(`dailydevops/pipelines/.github/workflows/build-dotnet-single.yml`) that builds and tests
`Analyzer.slnx`; there's no separate lint job to replicate locally beyond `csharpier format .` and the
analyzers themselves (this repo eats its own dog food plus Meziantou/Roslynator/SonarAnalyzer/NetAnalyzers,
see `Directory.Packages.props`).

## Architecture

**Adding a new rule** touches these pieces, all wired together by convention:

1. `DiagnosticIds.cs` — register the `NExxxx` constant, grouped by category with a comment banner.
2. `DiagnosticCategories.cs` / `DiagnosticDescriptors.cs` — the descriptor, category, severity, and
   `HelpLinkUri` (built from `DiagnosticIds.HelpLink`, which points at `docs/rules/NExxxx.md`).
3. `src/NetEvolve.Analyzer/<Category>/<Rule>Analyzer.cs` — the `DiagnosticAnalyzer`, placed under the
   folder matching its category (this folder placement is itself enforced by NE0002 on this codebase).
4. `<Rule>CodeFixProvider.cs` next to the analyzer, if a mechanical fix exists. Simple fixes use the
   default `WellKnownFixAllProviders.BatchFixer`; fixes that need solution-level operations (renaming
   /adding documents, whole-file rewrites) instead construct a `Providers.SequentialFixAllProvider`,
   passing a factory for a fresh analyzer instance — see NE0001 (rename/move) and NE0003 (flatten) for
   the pattern. It re-resolves diagnostics from the accumulating solution after every single fix until
   no more apply, because overlapping edits and rename→move flips can't compose through the batch fixer.
5. `AnalyzerReleases.Unshipped.md` — add the entry (moves to `AnalyzerReleases.Shipped.md` on release,
   per the standard Roslyn analyzer-release-tracking convention, RS2008).
6. `docs/rules/NExxxx.md` — one page per rule: Cause / Rule description / How to fix violations /
   Configuration / Suppress, matching the existing rules' structure exactly (see any file in
   `docs/rules/` as the template).
7. Tests in **both** `test/NetEvolve.Analyzer.Tests.Unit` and
   `test/NetEvolve.Analyzer.Tests.Integration`, mirroring the `<Category>/` folder layout. Unit tests use
   `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` / `...CodeFix.Testing` verifiers
   (`test/.../Verifiers/CSharpAnalyzerVerifier.cs`, `CSharpCodeFixVerifier.cs`) plus a per-rule
   `*Verifier.cs` / `*CodeFixVerifier.cs` / `*FixAllRunner.cs` helper.

**MSBuild-driven configuration**: rules that need MSBuild state (not just syntax/semantics) read it
through `AnalyzerConfigOptions`, using keys centralized in `BuildProperty.cs` (all prefixed
`build_property.`). The matching `CompilerVisibleProperty` declarations that make those MSBuild
properties visible to the compiler ship in `build/NetEvolve.Analyzer.props`, which is packed to
`build/NetEvolve.Analyzer.props` in the consumer's project — editing what a rule reads means updating
both files. Two cross-cutting switches gate whole rule groups: `PublishSingleFile` (auto-disables
file-organization rules for single-file deployments) and
`NetEvolveAnalyzerDisableFileOrganizationRules` (explicit opt-out).

**Shared helpers** (`Helpers/`):
- `LanguageVersionGate` — the null-check code fixes (NE0004–NE0006) target `netstandard2.0` and must
  never offer a pattern the consuming project's `LangVersion` can't compile (`is null` needs C# 7.0,
  `is not null` needs C# 9.0); always gate a new pattern-based fix through this before registering it.
- `NullCheckOperand` — shared logic for identifying which operand of a null comparison is "patternable"
  (non-pointer reference type, `Nullable<T>`, or unconstrained type parameter) across NE0004–NE0006.
- `FolderNamespace` — derives the expected namespace from `RootNamespace` + a file's folder path
  relative to `ProjectDir`; used by NE0002 and as the flatten target in NE0003.
- `Builders/NamespaceFileBuilder` — assembles a new file's content (namespace, usings, type) when a code
  fix moves or extracts a type/namespace into its own file.

**Naming conventions across all rules**: generated code is always skipped; comparisons/checks inside
`Expression<T>` trees are always left alone (the `is`-pattern rewrites aren't legal there); diagnostics
are reported regardless of `LangVersion`, but fixes are only registered when the language version
supports the resulting syntax.

## Conventions (see CONTRIBUTING.md for the full list)

- Conventional Commits are required (`feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `build`, `ci`,
  `perf`, `revert`, breaking changes via `!` or a `BREAKING CHANGE:` footer).
- English only, everywhere (code, docs, commits).
- Package versions are centralized in `Directory.Packages.props` (`ManagePackageVersionsCentrally`);
  never add a `Version` attribute on a `PackageReference` in a project file.
- `.editorconfig` at the repo root is shared org-wide and explicitly marked "DO NOT CHANGE — open a PR
  in `dotnet-engineering` instead."
- `docs/rules/NExxxx.md` and the analyzer's own `README.md`/solution `README.md` should stay in sync
  with the shipped rule set whenever rules are added, removed, or reconfigured.
