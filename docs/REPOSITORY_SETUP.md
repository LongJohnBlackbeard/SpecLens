# Repository Setup

This document captures recommended GitHub settings for maintaining a public repository.

## Security and Automation
- Enable Dependabot alerts.
- Enable secret scanning and push protection.
- Enable code scanning (CodeQL) and keep the workflow enabled.
- Enable dependency review for pull requests.
- Enable the CI and Release workflows.

## Branch Protection
- Require pull request reviews.
- Require status checks to pass before merging.
- Require linear history if preferred.
- Require signed commits if your team uses signing.

## Pull Requests
- Use the PR template in .github/pull_request_template.md.
- Include build/test commands and JDE runtime notes in every PR.

## Releases
- Tag releases and attach release notes.
- Document breaking changes and migration steps.
- Use pre-release tags (e.g., v1.2.0-beta.1) to publish preview builds.
