# JDE C API Workflows (JdeClient.Core)

This document maps JdeClient.Core features to the underlying JDE C APIs used
through `jdekrnl.dll` (and related DLLs). Use it when debugging behavior or
extending the library.

## High-Level Map

| Workflow | Primary APIs | Notes |
| --- | --- | --- |
| Session bootstrap | `JDB_GetEnv`, `JDB_GetLocalClientEnv`, `JDB_InitEnv`, `JDB_InitUser` | Runs on the JdeSession worker thread. Handles are freed with `JDB_FreeUser` / `JDB_FreeEnv`. |
| Object catalog (F9860) | `JDB_OpenTable`, `JDB_SetSelection`, `JDB_SelectKeyed`, `JDB_Fetch`, `JDB_GetTableColValue` | Filters for object type/name/description are pushed into selection (LIKE uses `%`). |
| Table queries | `JDB_OpenTable`, `JDB_SetSelectionX`, `JDB_SelectKeyed`, `JDB_Fetch` | `QueryTableAsync` and `QueryTableStream` share this path. |
| Table row count | `JDB_SelectKeyedGetCount` | Selection is applied first; no row data is fetched. |
| Table specs | `JDBRS_GetTableSpecsByName`, `JDBRS_FreeTableSpecs` | Used by `GetTableInfoAsync` / `GetTableIndexesAsync`. |
| Business view specs | `JDBRS_GetBOBSpecs`, `JDBRS_FreeBOBSpecs` | Used by `GetBusinessViewInfoAsync`. |
| Event rules tree (BSFN) | `JDB_OpenTable`, `JDB_SelectKeyed`, `JDB_Fetch` | Reads F9862 to map function name -> EVSK + DSTMPL. |
| Event rules tree (APPL/UBE/TBLE) | `JDB_OpenTable`, `JDB_SetSelectionX`, `JDB_SelectKeyed`, `JDB_Fetch` | Reads F98740 (GBRLINK). |
| Event rules XML | `jdeSpecOpen*`, `jdeSpecSelectKeyed`, `jdeSpecFetch`, `jdeSpecInitXMLConvertHandle`, `jdeSpecConvertToXML_UTF16`, `jdeSpecFreeData` | Reads F98741 (GBRSPEC). |
| Data structure XML | `jdeSpecOpen*`, `jdeSpecFetchSingle`, `jdeSpecSelectKeyed`, `jdeSpecFetch`, `jdeSpecInitXMLConvertHandle` | Reads F98743 (DSTMPL). |
| Project metadata | `JDB_OpenTable`, `JDB_SetSelectionX`, `JDB_SelectKeyed`, `JDB_Fetch` | F98220/F98221/F98222. PATHCD is optional and often contains legacy values. |
| Spec repositories (zip) | `jdeSpecOpenRepository`, `jdeSpecOpenFile`, `jdeSpecCloseRepository` | Used when reading spec ZIP repositories (e.g., manifest). |
| OMW export (future) | `OMWCallSaveObjectToRepositoryEx` (`jdeomw.dll`) | Available in the runtime but not yet wired into JdeClient.Core. |

## Notes by Workflow

### Session and Threading

JdeClient.Core uses a dedicated worker thread for all native calls. This keeps
JDE state isolated and avoids thread affinity problems in native libraries.

### Object Catalog (F9860)

- Filters are pushed to the runtime using `JDB_SetSelection`.
- Column values are read with `JDB_GetTableColValue` to avoid manual buffer
  allocations.

### Table Queries and Counting

- Filters are pushed via `JDB_SetSelectionX`.
- `QueryTableCountAsync` uses `JDB_SelectKeyedGetCount` for a fast count-only
  path (no row fetch loop).

### Specs

- Table specs come from `JDBRS_GetTableSpecsByName`.
- Business view specs come from `JDBRS_GetBOBSpecs`.
- Event rules and data structures use the spec encapsulation APIs:
  `jdeSpecOpen*`, `jdeSpecSelectKeyed`, `jdeSpecFetch`, and the XML conversion
  helpers (`jdeSpecInitXMLConvertHandle`, `jdeSpecConvertToXML_UTF16`).

### Project Metadata (OMW)

Projects and project objects are table-driven (F98220 / F98221 / F98222). PATHCD
is not a reliable primary key in practice; use it only as an optional filter
when it matches your target environment.

### OMW Export (Future)

The JDE runtime ships an OMW export API (`OMWCallSaveObjectToRepositoryEx`) that
creates repository artifacts (including spec XML). This is a candidate for
future project or object export workflows, but it is not yet exposed in
JdeClient.Core.

