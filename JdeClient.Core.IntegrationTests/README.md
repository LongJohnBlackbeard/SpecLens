# JdeClient.Core.IntegrationTests

[![CI](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml) [![CodeQL](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml) [![Dependency Review](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml) [![Release](https://img.shields.io/github/v/release/LongJohnBlackbeard/SpecLens?sort=semver)](https://github.com/LongJohnBlackbeard/SpecLens/releases)

Integration tests for JdeClient.Core that run against a live JDE runtime.

**Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by Oracle or JD Edwards.

## Requirements
- JDE Fat Client installed and logged in
- activConsole.exe running
- .NET SDK (see global.json)

## Run
```bash
dotnet test JdeClient.Core.IntegrationTests/JdeClient.Core.IntegrationTests.csproj
```

## Notes
- These tests require an active JDE session.

