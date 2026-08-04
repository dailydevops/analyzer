# NetEvolve.Analyzer

[![NuGet Version](https://img.shields.io/nuget/v/NetEvolve.Analyzer.svg)](https://www.nuget.org/packages/NetEvolve.Analyzer/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetEvolve.Analyzer.svg)](https://www.nuget.org/packages/NetEvolve.Analyzer/)
[![License](https://img.shields.io/github/license/dailydevops/analyzer.svg)](https://github.com/dailydevops/analyzer/blob/main/LICENSE)

Roslyn analyzers and code fixes for .NET, organized by the standard [Microsoft diagnostic
categories](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/categories). It enforces
consistent file organization and safer null-check idioms in C# code, with an automatic fix offered for
every rule that has one.

## Features

- **File organization** - one type per file, namespace matches folder structure, single namespace per file
- **Null-check idioms** - `is null` / `is not null` patterns instead of `== null`, `!= null`, or `is object`
- **Code fixes** - a fix is registered for every rule, gated to the language version it requires
- **Batch fixes** - a shared sequential Fix-All provider applies fixes across a document, project, or solution
- **Configurable** - MSBuild properties tune or fully disable the file-organization rules
- **Zero footprint** - a development dependency only; no runtime assembly is added to your project

## Installation

### NuGet Package Manager

```powershell
Install-Package NetEvolve.Analyzer
```

### .NET CLI

```bash
dotnet add package NetEvolve.Analyzer
```

### PackageReference

```xml
<PackageReference Include="NetEvolve.Analyzer" Version="x.x.x" PrivateAssets="all" />
```

The package ships the analyzer assembly built separately against four Roslyn API versions (see
[Supported Roslyn versions](#supported-roslyn-versions)) and adds no runtime dependencies to your
project; `PrivateAssets="all"` keeps it from flowing to your own package consumers.

## Quick Start

No code changes are required. Once the package reference is added, the analyzers run on every build
and report diagnostics under the `NE` prefix, for example:

```text
warning NE0004: Use the 'is null' pattern instead of '== null'
```

Apply the offered code fix in your editor, or fix every occurrence at once with `dotnet format`:

```bash
dotnet format analyzers --diagnostics NE0004
```

## Usage

### Basic example

Given:

```csharp
if (value == null)
{
    return;
}
```

NE0004 reports the comparison and offers a code fix that rewrites it to:

```csharp
if (value is null)
{
    return;
}
```

### File organization example

NE0001 and NE0002 keep a file's name and namespace aligned with its type and folder. A file at
`Shapes/Circle.cs` declaring `namespace Geometry.Polygons;` is flagged by NE0002 with a fix that
rewrites the namespace to `Geometry.Shapes` (the folder-derived value, anchored at `RootNamespace`).

## Configuration

All configuration is done through standard MSBuild properties in your project file — no `.editorconfig`
entries are required.

```xml
<PropertyGroup>
  <!-- Allow grouping generic overloads (Result, Result<T>, …) in one file named 'Result.cs'. -->
  <NetEvolveAnalyzerGroupGenericOverloads>true</NetEvolveAnalyzerGroupGenericOverloads>

  <!-- Turn the file/namespace organization rules (NE0001-NE0003) off entirely. -->
  <NetEvolveAnalyzerDisableFileOrganizationRules>true</NetEvolveAnalyzerDisableFileOrganizationRules>
</PropertyGroup>
```

The file-organization rules are also disabled automatically for single-file deployments
(`PublishSingleFile=true`).

To suppress an individual diagnostic instead:

```csharp
#pragma warning disable NE0001
#pragma warning restore NE0001
```

## Diagnostics

Every rule uses the `NE` identifier prefix and is documented under
[`docs/rules`](https://github.com/dailydevops/analyzer/tree/main/docs/rules), with its cause, rule
description, fix guidance, and configuration options.

| Rule | Category | Description | Code fix |
|------|----------|--------------|----------|
| [NE0001](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0001.md) | Maintainability | Declare one type per file with a matching file name | Yes |
| [NE0002](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0002.md) | Maintainability | Namespace should match the folder structure | Yes |
| [NE0003](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0003.md) | Maintainability | Declare a single namespace per file | Yes |
| [NE0004](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0004.md) | Usage | Use the `is null` pattern instead of `== null` | Yes |
| [NE0005](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0005.md) | Usage | Use the `is not null` pattern instead of `!= null` | Yes |
| [NE0006](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0006.md) | Usage | Use the `is not null` pattern instead of `is object` | Yes |
| [NE0007](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0007.md) | Documentation | Use `<see langword="..."/>` instead of `<c>...</c>` or `<code>...</code>` for C# keywords | Yes |
| [NE0008](https://github.com/dailydevops/analyzer/blob/main/docs/rules/NE0008.md) | Documentation | Use `<see cref="..."/>` instead of `<c>...</c>` or `<code>...</code>` for native type names | Yes |

A `DiagnosticSuppressor` (NES0001) also ships alongside NE0007: it suppresses Meziantou.Analyzer's `MA0154`
wherever NE0007 already reports the same location, so a consumer running both analyzers doesn't get the
same violation twice — see [`docs/rules/NE0007.md`](../../docs/rules/NE0007.md#interaction-with-meziantouanalyzers-ma0154).

See [`AnalyzerReleases.Shipped.md`](AnalyzerReleases.Shipped.md) for the list of rules included in the
current release, and [`AnalyzerReleases.Unshipped.md`](AnalyzerReleases.Unshipped.md) for rules staged
for the next one.

## Requirements

- A project compiling with a Roslyn-based compiler (any currently supported .NET SDK)
- No target-framework restriction on the consuming project — the analyzer runs against the compiler,
  not your project's target framework

## Supported Roslyn versions

The analyzer's source is built separately against four Roslyn API (`Microsoft.CodeAnalysis.CSharp`)
versions — 4.4.0, 4.7.0, 4.14.0, and the latest, 5.6.0 — each packed into its own
`analyzers/dotnet/roslynX.Y/cs` folder. .NET SDKs from 8.0.400 onward select the highest version they
support automatically; you don't need to configure anything.

SDKs that predate this selection feature don't load any analyzer build from this package (rather than
an incorrect or duplicated one) — in practice this only affects unpatched, out-of-support SDKs, since
the selection logic lives in the NuGet client bundled with the SDK and has been serviced into every
currently maintained release, including older LTS versions like .NET 6.

## Documentation

For the full solution documentation, see the [repository README](https://github.com/dailydevops/analyzer/blob/main/README.md).

## Contributing

Contributions are welcome! Please read the [Contributing Guidelines](https://github.com/dailydevops/analyzer/blob/main/CONTRIBUTING.md) before submitting a pull request.

## Support

- **Issues**: Report bugs or request features on [GitHub Issues](https://github.com/dailydevops/analyzer/issues)
- **Documentation**: Read the full documentation at [https://github.com/dailydevops/analyzer](https://github.com/dailydevops/analyzer)

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/dailydevops/analyzer/blob/main/LICENSE) file for details.

---

> [!NOTE]
> **Made with ❤️ by the NetEvolve Team**
> Visit us at [https://www.daily-devops.net](https://www.daily-devops.net) for more information about our services and solutions.
