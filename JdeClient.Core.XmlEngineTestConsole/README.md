# JdeClient.Core.XmlEngineTestConsole

[![CI](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml) [![CodeQL](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml) [![Dependency Review](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml) [![Release](https://img.shields.io/github/v/release/LongJohnBlackbeard/SpecLens?sort=semver)](https://github.com/LongJohnBlackbeard/SpecLens/releases)

Console harness for validating XML spec conversion and event rules formatting.

**Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by Oracle or JD Edwards.

## Requirements
- JDE Fat Client installed and logged in
- activConsole.exe running

## Run
```bash
dotnet run --project JdeClient.Core.XmlEngineTestConsole/JdeClient.Core.XmlEngineTestConsole.csproj
```

## Notes
- This project uses the JDE C APIs for spec access.
- Keep test inputs free of secrets.

