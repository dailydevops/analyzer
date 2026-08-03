# NetEvolve.Analyzer

Roslyn analyzers and code fixes for .NET, organized by the standard
[Microsoft diagnostic categories](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/categories)
(Design, Documentation, Globalization, Interoperability, Maintainability, Naming, Performance,
Reliability, Security, Style, Usage).

## Installation

```shell
dotnet add package NetEvolve.Analyzer
```

The package is a development dependency: it ships only the analyzer assembly under
`analyzers/dotnet/cs` and adds no runtime dependencies to your project.

## Diagnostics

Every rule uses the `NE` identifier prefix and is documented under
[`docs/rules`](https://github.com/dailydevops/analyzer/tree/main/docs/rules). See
[`AnalyzerReleases.Shipped.md`](https://github.com/dailydevops/analyzer/blob/main/src/NetEvolve.Analyzer/AnalyzerReleases.Shipped.md)
for the list of shipped rules.

## License

Licensed under the [MIT License](https://github.com/dailydevops/analyzer/blob/main/LICENSE).
