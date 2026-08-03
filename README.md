# analyzer

[![License](https://img.shields.io/github/license/dailydevops/analyzer.svg)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/dailydevops/analyzer/cicd.yml?branch=main)](https://github.com/dailydevops/analyzer/actions)
[![Contributors](https://img.shields.io/github/contributors/dailydevops/analyzer.svg)](https://github.com/dailydevops/analyzer/graphs/contributors)

A .NET solution that ships `NetEvolve.Analyzer`, a Roslyn analyzer and code-fix package enforcing
maintainability and usage conventions in C# codebases. It targets teams that want consistent file
organization (one type per file, namespace-matches-folder) and safer null-check idioms enforced
automatically at build time, with automatic fixes wherever a fix is unambiguous.

## Overview

The solution is a single analyzer package plus its test suites. The analyzer assembly targets
`netstandard2.0` so it loads in every Roslyn host (Visual Studio, MSBuild, the `dotnet` CLI) and ships
as a development-only dependency: no runtime assemblies are added to a consuming project.

Rules are organized by the standard [Microsoft diagnostic
categories](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/categories) (Design,
Documentation, Globalization, Interoperability, Maintainability, Naming, Performance, Reliability,
Security, Style, Usage) and use the `NE` diagnostic prefix. Each shipped rule is documented under
[`docs/rules`](docs/rules) and, where a mechanical fix exists, comes with a code-fix provider.

## Projects

### Analyzer

- **[NetEvolve.Analyzer](src/NetEvolve.Analyzer/README.md)** - Roslyn analyzers and code fixes for
  maintainability and usage rules, packaged as a NuGet analyzer.

### Tests

- **NetEvolve.Analyzer.Tests.Unit** - Unit tests for individual analyzers, code fixes, and helpers.
- **NetEvolve.Analyzer.Tests.Integration** - Integration tests exercising the analyzers against real
  compilations.

## Features

- File organization rules - one type per file, namespace-matches-folder, single namespace per file
- Null-check idiom rules - `is null` / `is not null` patterns instead of `==`/`!=`/`is object`
- Code fixes for every rule where a safe, mechanical fix exists
- Configurable via standard MSBuild properties, with an opt-out switch for the file-organization rules
- Automatic exemption for single-file deployments (`PublishSingleFile=true`)
- Zero runtime dependencies added to consuming projects (development dependency only)

## Getting Started

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or higher
- [Git](https://git-scm.com/) for version control
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) (recommended)

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/dailydevops/analyzer.git
   cd analyzer
   ```

2. Restore dependencies:

   ```bash
   dotnet restore
   ```

3. Build the solution:

   ```bash
   dotnet build
   ```

4. Run tests to verify installation:

   ```bash
   dotnet test
   ```

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run a specific test project
dotnet test test/NetEvolve.Analyzer.Tests.Unit
dotnet test test/NetEvolve.Analyzer.Tests.Integration
```

### Code Formatting

```bash
# Format code using CSharpier
csharpier format .
```

### Project Structure

```txt
src/                              # Production code
└── NetEvolve.Analyzer/          # Roslyn analyzers, code fixes, and helpers, grouped by diagnostic category

test/                             # Test projects
├── NetEvolve.Analyzer.Tests.Unit/         # Unit tests for analyzers, code fixes, and helpers
└── NetEvolve.Analyzer.Tests.Integration/  # Integration tests against real compilations

docs/
└── rules/                        # One Markdown file per diagnostic (NE0001.md, NE0002.md, ...)
```

## Architecture

Each rule lives under `src/NetEvolve.Analyzer/<Category>/`, matching the diagnostic category it
belongs to (for example `Maintainability/` or `Usage/`). Diagnostic identifiers are registered
centrally in `DiagnosticIds.cs`, and MSBuild property keys read by rules are registered in
`BuildProperty.cs`. Where a rule can be fixed mechanically, a matching `*CodeFixProvider` sits next to
the analyzer, and batch fixes across a project share the generalized `SequentialFixAllProvider`.

Analyzer release tracking follows the standard Roslyn convention via
`AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` in the project.

## Contributing

We welcome contributions from the community! Please read our [Contributing Guidelines](CONTRIBUTING.md)
before submitting a pull request.

Key points:

- Follow the [Conventional Commits](https://www.conventionalcommits.org/) format for commit messages
- Write tests for new functionality
- Follow existing code style and conventions
- Update documentation as needed

## Code of Conduct

This project adheres to the Contributor Covenant [Code of Conduct](CODE_OF_CONDUCT.md). By
participating, you are expected to uphold this code. Please report unacceptable behavior to
[info@daily-devops.net](mailto:info@daily-devops.net).

## Documentation

- **[Rule documentation](docs/rules)** - One page per diagnostic, with cause, rule description,
  fix guidance, and configuration
- **[Contributing Guidelines](CONTRIBUTING.md)** - How to contribute to this project
- **[Code of Conduct](CODE_OF_CONDUCT.md)** - Community standards and expectations
- **[License](LICENSE)** - Project licensing information

## Versioning

This project uses [GitVersion](https://gitversion.net/) for automated semantic versioning based on Git
history and [Conventional Commits](https://www.conventionalcommits.org/). Version numbers are
automatically calculated during the build process.

## Support

- **Issues**: Report bugs or request features on [GitHub Issues](https://github.com/dailydevops/analyzer/issues)
- **Documentation**: Read the full documentation in this repository

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

> [!NOTE]
> **Made with ❤️ by the NetEvolve Team**
> Visit us at [https://www.daily-devops.net](https://www.daily-devops.net) for more information about our services and solutions.
