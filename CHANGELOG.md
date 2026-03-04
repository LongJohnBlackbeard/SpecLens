# Changelog

All notable changes to this project will be documented in this file.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-prototype.11] - 2026-03-04

### Changed

- **Breaking:** query rows now use `JdeRow` (`IReadOnlyDictionary<string, string>`) instead of `Dictionary<string, object>`.
- Updated `JdeQueryResult.Rows` to `List<JdeRow>`.
- Updated `JdeQueryStream` to stream `JdeRow` and to be consumed directly via `foreach (var row in stream)`.
- Updated query engine and client internals to use typed rows end to end.
- Updated downstream consumers (`SpecLens.Avalonia` and `JdeClient.TestConsole`) for typed row access.

### Documentation

- Updated `JdeClient.Core/README.md` query examples to iterate `JdeQueryStream` directly and document `JdeRow`.

### Tests

- Updated unit and integration tests to validate typed query rows (`JdeRow`) and string-based value access.

## [0.1.0-prototype.10] - 2026-03-04

### Changed

- Updated `SearchDataDictionariesAsync(...)` to return fully hydrated `JdeDataDictionaryDetails` entries only.
- Wildcard search still discovers candidates by `DTAI` from `F9200`, then hydrates details/text via dictionary APIs (`DDDICT`/`DDTEXT`).
- Removed summary-row fallback from search results when hydration is unavailable.

### Documentation

- Clarified `JdeClient.Core/README.md` Data Dictionary workflow notes to describe strict hydration-only search behavior.

### Tests

- Updated unit tests to validate hydrated search results and no-row behavior when candidate hydration fails.
- Updated integration tests so wildcard search asserts hydrated data dictionary detail fields.

## [0.1.0-prototype.9] - 2026-03-04

### Changed

- Consolidated Data Dictionary APIs into `GetDataDictionariesAsync(...)`, `GetDataDictionaryAsync(...)`, and `SearchDataDictionariesAsync(...)`.
- Implemented the wildcard search workflow on `DTAI` via `SearchDataDictionariesAsync(...)` and standardized results on `JdeDataDictionaryDetails`.
- Removed legacy Data Dictionary APIs: `GetDataDictionaryTitlesAsync`, `GetDataDictionaryDescriptionsAsync`, `GetDataDictionaryItemNamesAsync`, and `GetDataDictionaryDetailsAsync`.
- Removed legacy Data Dictionary models: `JdeDataDictionaryTitle`, `JdeDataDictionaryItemName`, and `JdeDataDictionarySearchResult`.

### Documentation

- Updated `JdeClient.Core/README.md` with the consolidated Data Dictionary workflow and migration notes.

### Tests

- Updated Data Dictionary unit and integration tests to validate the consolidated API surface.
- Added unit-test coverage for `GetDataDictionaryAsync(...)` not-found behavior and `SearchDataDictionariesAsync(...)` negative `maxRows` validation.

## [0.1.0-prototype.8] - 2026-03-03

### Changed

- Aligned table/view spec and query source-resolution workflows with FAT-client logs, including stricter separation of path code versus runtime data source behavior.
- Updated spec-loading behavior so explicit spec-source requests are strict (no runtime-default fallback), while default public API workflows still use standard resolution when overrides are not supplied.
- Expanded system-table handling for `F9860`, `F98611`, and `F00942` so explicit data-source overrides are ignored for those system contexts.
- Added explicit-source table-spec retrieval path using `JDB_OpenTable` + `JDBRS_GetTableSpecsFromHandle`.
- Reworked business-view spec retrieval to use `jdeSpecOpen*` source-aware flows and parse packed/XML payloads consistently.
- Updated internal metadata/query engine interfaces and call chains to carry explicit spec-source override options through table/view metadata APIs.

### Fixed

- Corrected Avalonia startup compatibility after ReactiveUI API changes by updating `UseReactiveUI` initialization to the new builder-callback signature.
- Fixed regressions where explicit object/spec source selections could be routed through unintended fallback paths.

### Tests

- Added and updated unit tests covering path-code token parsing, `F00942` path-code-to-data-source resolution, system-table override behavior, `LOCAL` handling, and strict explicit-source metadata retrieval paths.

### Dependencies

- Bumped TUnit from `1.17.29` to `1.18.0`.
- Updated `actions/upload-artifact` from v6 to v7 in GitHub workflows.

## [0.1.0-prototype.7] - 2026-02-26

### Fixed

- Release workflow now publishes `JdeClient.Core` NuGet packages to GitLab by enumerating concrete `.nupkg` files, avoiding PowerShell wildcard expansion issues on `windows-latest`.

## [0.1.0-prototype.6] - 2026-02-26

### Added

- `JdeClient.Core` release workflow now publishes NuGet packages directly to the GitLab package registry.
- New `JdeClient.GetObjectDescriptionAsync(...)` helper for exact object-name description lookup from F9860.

### Changed

- `JdeProjectObjectInfo` now includes `Description` mapped from OMW object data.
- Added unit-test coverage for object-description lookup and project-object description mapping.

## [0.1.0-prototype.5] - 2026-02-12

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
