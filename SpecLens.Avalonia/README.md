# SpecLens.Avalonia

[![CI](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml) [![CodeQL](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml) [![Dependency Review](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml) [![Release](https://img.shields.io/github/v/release/LongJohnBlackbeard/SpecLens?sort=semver)](https://github.com/LongJohnBlackbeard/SpecLens/releases)

Avalonia desktop UI for exploring JDE metadata, specs, and event rules.

**Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by Oracle or JD Edwards.

## Requirements
- JDE Fat Client installed and logged in
- activConsole.exe running

## Run
```bash
dotnet run --project SpecLens.Avalonia/SpecLens.Avalonia.csproj
```

## Notes
- Uses JdeClient.Core for JDE access.
- Uses ViewportGrid.Core and ViewportGrid.Data for grid rendering.

