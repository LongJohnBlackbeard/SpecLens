# Spec Lens

[![CI](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml) [![CodeQL](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml) [![Dependency Review](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml) [![Release](https://img.shields.io/github/v/release/LongJohnBlackbeard/SpecLens?sort=semver)](https://github.com/LongJohnBlackbeard/SpecLens/releases) [![codecov](https://codecov.io/gh/LongJohnBlackbeard/SpecLens/branch/main/graph/badge.svg)](https://codecov.io/gh/LongJohnBlackbeard/SpecLens) 
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

Spec Lens is a .NET solution for working with JDE objects and specifications through the native JDE C APIs. This 
repository is open source, so anyone interested is welcome to contribute.

The repo includes a reusable client library, test harnesses, and a desktop UI. The packaged application is 
distributed through releases today; in the future, it could be installed and updated via an installer or a similar deployment method.

**Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by Oracle or JD Edwards.

**Disclaimer:** This project is in early development and bugs and missing features are expected. Any bugs, enhancments, missing features 
can be reported through the Issues tab.

## Features
- Object catalog search (F9860)
- Table and View Spec and Data browsing
- Business function (NER only for now) event rules browsing
- Custom viewport grid components (prioritizing speed over UI)
- Grid Sorting, Sequencing, and Column Freezing
- Dark/Light theme support
- Custom Syntax Highlighting for ER

## Future Features
- All Object Event Rules Browsing
- Specifying Object/Spec Location (Local, DV920, PY920, etc)
- Run Business Functions
- ER Search
- DD Search
- UDC Search
- Expanded Theme Customization
- Expanded Syntax Highlighting Customization
- Grid Export (Current Grid vs Query) into multiple formats.
- Improved Tab Sequence Navigation
- Customizable Hot Keys
- and Much more

## Requirements
- Windows 64-bit (win-x64)
- JDE Fat Client installed and logged in
- activConsole.exe running

## A note on file size
- The Windows .exe is currently ~75 MB. This is expected because the app is published as self-contained.
- Avalonia runs on .NET, and a self-contained publish bundles the .NET runtime and required framework libraries directly into the executable so users don’t need to install .NET separately.
- As a result, most of the 75 MB (roughly 65–85%) is the bundled .NET runtime/framework, not the application code itself.

## Associated Documents
- [AI_USE_POLICY](AI_USE_POLICY.md)
- [AGENTS](AGENTS.md)
- [Testing Best Practices](TESTING_BEST_PRACTICES.md)
- [Testing Examples](TESTING_EXAMPLES.md)
- [Logging](docs/LOGGING.md)
- [Repository Setup](docs/REPOSITORY_SETUP.md)
- [JDE API Workflows](docs/JDE_API_WORKFLOWS.md)
- [JdeClient.Core README](JdeClient.Core/README.md)

## Build and run
```bash
dotnet build SpecLens.sln

dotnet run --project SpecLens.Avalonia/SpecLens.Avalonia.csproj

dotnet run --project JdeClient.TestConsole/JdeClient.TestConsole.csproj
```

## Logging
Spec Lens has separate logs for the desktop app and the JDE client debug output.
Both are disabled by default and can be enabled from the Settings window.
See docs/LOGGING.md for how to enable logging, where files are written, and what to share.

## Testing
Unit tests do not require a JDE runtime:
```bash
dotnet test JdeClient.Core.UnitTests/JdeClient.Core.UnitTests.csproj
```

Coverage (TUnit):
```bash
New-Item -ItemType Directory -Force TestResults\coverage | Out-Null
dotnet test JdeClient.Core.UnitTests/JdeClient.Core.UnitTests.csproj -c Release -- --results-directory $PWD\TestResults --coverage --coverage-output $PWD\TestResults\coverage\coverage.cobertura.xml --coverage-output-format cobertura --report-trx --report-trx-filename JdeClient.Core.UnitTests.trx
```
Coverage output (Cobertura XML) is written to `TestResults/coverage/coverage.cobertura.xml` at the repo root.

### Complexity & CRAP standards
We use ReportGenerator's **Risk Hotspots** view (CRAP + Cyclomatic Complexity) to guide improvements.

- **Cyclomatic Complexity (CC):** target ≤ 15 for new/modified methods; review/refactor when > 20.
- **CRAP score:** target ≤ 30 for new/modified methods; review/refactor when > 50.
- When touching an existing hotspot, add tests or refactor to reduce CC/CRAP where practical.

Integration tests require the JDE runtime and are manual-only (not run in CI workflows):
```bash
dotnet test JdeClient.Core.IntegrationTests/JdeClient.Core.IntegrationTests.csproj
```

## Repository layout
- JdeClient.Core: core library that wraps JDE native APIs
- JdeClient.TestConsole: interactive console harness
- SpecLens.Avalonia: desktop UI
- JdeClient.Core.UnitTests: unit tests
- JdeClient.Core.IntegrationTests: integration tests that require JDE runtime
- JdeClient.Core.XmlEngineTestConsole: XML engine test harness
- ViewportGrid.Core and ViewportGrid.Data: custom grid components

## Security
See SECURITY.md for reporting guidance. This project does not store secrets or connect directly to databases. All queries and data access run through the JDE C APIs.
Maintainers: see docs/REPOSITORY_SETUP.md for required GitHub security settings.

## Contributing
See CONTRIBUTING.md for workflow and requirements.

## License
Licensed under the GNU GPLv3. See LICENSE.
