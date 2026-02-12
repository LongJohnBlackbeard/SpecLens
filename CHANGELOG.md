# Changelog

All notable changes to this project will be documented in this file.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

# ## [0.1.0-prototype.2] - 2026-02-12

### Added
- First prototype release of the Spec Lens solution.
- `JdeClient.Core` library for accessing JD Edwards through native JDE C APIs.
- Avalonia desktop app for object browsing, table/view specs, and event-rules exploration.
- Object catalog search (F9860) with object-type and pattern filters.
- Table/business-view metadata lookup and query APIs (count, buffered query, and streaming).
- Event-rules tree/XML/formatting support for business functions, applications, reports, and tables.
- Business function C source/header retrieval and in-app code viewing support.
- Location-aware object/spec retrieval (Local and path-code-based Object Librarian/Central Objects selection).
- OMW project/object lookup and project export to `.par`.
- UDC lookup support (`F0004`/`F0005`) and available path-code/data-source discovery APIs.
- Test console and XML engine console projects for local validation/debugging.
- GitHub automation for CI, CodeQL, dependency review, release drafting, and tag-based release publishing.
