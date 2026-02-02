# Contributing

Thanks for contributing. This project wraps JDE native APIs and requires a JDE runtime for integration tests.

## Prerequisites
- JDE Fat Client installed and logged in
- activConsole.exe running
- .NET SDK (see global.json)

## Development workflow
1. Create a feature branch.
2. Make focused changes with clear commits.
3. Update or add tests when behavior changes.
4. Update documentation when public behavior changes.

## GitHub workflow (VS Code / Rider / Visual Studio)
1. Sync `main` and create a branch: `git checkout main` then `git pull`.
2. Create a feature branch: `git checkout -b feature/short-description`.
3. Commit small, logical changes with imperative messages.
4. Push and open a pull request for review.
5. Resolve feedback, keep the branch up to date, and squash only if requested.

## Build
```bash
dotnet build SpecLens.sln
```

## Tests
Unit tests (no JDE runtime required):
```bash
dotnet test JdeClient.Core.UnitTests/JdeClient.Core.UnitTests.csproj
```

Integration tests (manual-only; JDE runtime required):
```bash
dotnet test JdeClient.Core.IntegrationTests/JdeClient.Core.IntegrationTests.csproj
```

## Code style
- C# uses 4-space indentation.
- Use PascalCase for types/methods and camelCase for locals/parameters.
- Async APIs should be suffixed with Async and accept CancellationToken where applicable.

## Documentation
- Use XML docs for public APIs.
- Add inline comments for complex logic.

## Release notes
GitHub Releases pull notes from `CHANGELOG.md`. When you open a PR, apply a label
so changes show up in the right section (e.g., `feature`, `enhancement`, `bug`,
`fix`, `docs`, `breaking`, `refactor`, `chore`, `ci`, `build`).

## AI-assisted contributions
AI tooling is allowed, but contributors are responsible for validating all changes.
See AI_USE_POLICY.md for requirements.

## Security and secrets
- Do not commit secrets, tokens, or credentials.
- This project does not use database connections or store secrets.

## Pull requests
- Describe the change and why it is needed.
- List the commands you ran.
- Note any JDE environment requirements.
- Include screenshots for UI changes when applicable.
