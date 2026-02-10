# JdeClient.Core

[![CI](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/ci.yml) [![CodeQL](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/codeql.yml) [![Dependency Review](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml/badge.svg?branch=main)](https://github.com/LongJohnBlackbeard/SpecLens/actions/workflows/dependency-review.yml) [![Release](https://img.shields.io/github/v/release/LongJohnBlackbeard/SpecLens?sort=semver)](https://github.com/LongJohnBlackbeard/SpecLens/releases)

> Reusable .NET 8 library for accessing JD Edwards EnterpriseOne via P/Invoke to native C APIs

**Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by Oracle or JD Edwards.

## Overview

JdeClient.Core is a standalone class library that provides a clean, modern C# API for accessing JD Edwards EnterpriseOne (E1) via **P/Invoke to native C APIs** (jdekrnl.dll).

**Key Features:**
- Clean, async API - no P/Invoke details exposed
- Proper memory management - IDisposable pattern throughout
- Type-safe models - no dealing with pointers or marshaling
- Exception handling - JDE errors translated to .NET exceptions
- Reusable - use in any C# project (console, WPF, web API, etc.)


## Installation

### From Source
```bash
dotnet add reference path/to/JdeClient.Core/JdeClient.Core.csproj
```

### NuGet (Future)
```bash
dotnet add package JdeClient.Core
```

## Quick Start

### Prerequisites

- **.NET 8**
- **JDE EnterpriseOne Fat Client** installed and running (activConsole.exe)
- User logged into JDE
- JDE runtime available (jdekrnl.dll ships with the fat client; your host process must be able to resolve it)

### Basic Usage

```csharp
using JdeClient.Core;
using JdeClient.Core.Models;

// Create client
using var client = new JdeClient();

// Connect to JDE (requires fat client running)
await client.ConnectAsync();

// Get list of tables from Object Librarian (F9860)
var tables = await client.GetObjectsAsync(JdeObjectType.Table, maxResults: 100);

foreach (var table in tables)
{
    Console.WriteLine($"{table.ObjectName}: {table.Description}");
}

// Get table metadata
var tableInfo = await client.GetTableInfoAsync("F0101");
Console.WriteLine($"Table {tableInfo.TableName} has {tableInfo.Columns.Count} columns");

// Disconnect
await client.DisconnectAsync();
```

### Console App Example

```csharp
using JdeClient.Core;
using JdeClient.Core.Models;

try
{
    var options = new JdeClientOptions
    {
        EnableDebug = true,
        EnableSpecDebug = true,
        EnableQueryDebug = true
    };

    using var client = new JdeClient(options);

    Console.WriteLine("Connecting to JDE...");
    await client.ConnectAsync();
    Console.WriteLine("Connected!");

    Console.WriteLine("\nFetching business functions...");
    var bsfns = await client.GetObjectsAsync(
        JdeObjectType.BusinessFunction,
        maxResults: 20);

    foreach (var bsfn in bsfns)
    {
        Console.WriteLine($"  {bsfn.ObjectName} - {bsfn.Description}");
    }

    Console.WriteLine($"\nTotal: {bsfns.Count} business functions");
}
catch (JdeConnectionException ex)
{
    Console.WriteLine($"Connection error: {ex.Message}");
}
catch (JdeException ex)
{
    Console.WriteLine($"JDE error: {ex.Message}");
}
```

## Workflows

### Object Catalog Browsing (F9860)

```csharp
var objects = await client.GetObjectsAsync(
    JdeObjectType.All,
    searchPattern: "F48*",
    descriptionPattern: "*Work*",
    maxResults: 200);
```

### Table and Business View Specs

```csharp
var tableInfo = await client.GetTableInfoAsync("F0101");
var tableIndexes = await client.GetTableIndexesAsync("F0101");

var viewInfo = await client.GetBusinessViewInfoAsync("V0101A");
```

### Query Patterns

```csharp
// Count only
var count = await client.QueryTableCountAsync("F0101");

// Buffered query with filters
var filters = new[]
{
    new JdeFilter { ColumnName = "AN8", Value = "1001" }
};
var rows = await client.QueryTableAsync("F0101", filters, maxRows: 100);

// Streamed query for large result sets
var stream = client.QueryTableStream("F0101", maxRows: 0);
foreach (var row in stream.Rows)
{
    // process row
}
```

### User Defined Codes (F0004/F0005)

```csharp
// UDC types (F0004) - wildcards supported via "*"
var codeTypes = await client.GetUserDefinedCodeTypesAsync(
    productCode: "01",
    userDefinedCode: "S*");

// UDC values (F0005) - wildcards supported via "*"
var codes = await client.GetUserDefinedCodesAsync(
    productCode: "01",
    userDefinedCodeType: "ST",
    userDefinedCode: "A*",
    description: "Act*",
    description2: null);
```

### Event Rules (BSFN/NER/APPL/UBE/TBLE)

```csharp
// BSFN (Business Function)
var bsfns = await client.GetObjectsAsync(JdeObjectType.BusinessFunction, searchPattern: "B*");
var root = await client.GetEventRulesTreeAsync(bsfns[0]);

// NERs are stored under BSFN; filter for N* if you want NER objects.
var ners = await client.GetObjectsAsync(JdeObjectType.BusinessFunction, searchPattern: "N*");
var nerRoot = await client.GetEventRulesTreeAsync(ners[0]);

// XML for a specific event spec key (EVSK)
var xmlDocs = await client.GetEventRulesXmlAsync("EVS-...-...");
```

### Project Pre-Promotion Export (Status 28)

When a project is ready to promote, first locate it in F98220, then pull objects from F98222 and retrieve specs.

```csharp
// Example environment override (adjust to your setup)
// Optional user filter (supports "*")
var projects = await client.GetProjectsAsync(status: "28", dataSourceOverride: "JPY920", user: "AL*");

foreach (var project in projects)
{
    // Path code is optional; omit it when F98222 PATHCD contains legacy values
    var objects = await client.GetProjectObjectsAsync(
        project.ProjectName,
        pathCode: null,
        dataSourceOverride: "JPY920");

    foreach (var obj in objects)
    {
        // Use GetEventRulesTreeAsync/GetEventRulesXmlAsync for ER-backed objects,
        // and table/view metadata APIs for TBLE/BSVW specs.
    }
}
```

### Data Source Overrides

Most table queries accept a `dataSourceOverride`. Use this to target an environment (for example, `JPY920`).
Path code (for example, `PY920`) is a separate concept and should be used only when it reflects the target
environment in F98222.

### Wildcards

The following methods accept "*" wildcards and translate them to LIKE ("%") automatically:

- `GetProjectsAsync` (user filter)
- `GetUserDefinedCodeTypesAsync` (`SY`, `RT`)
- `GetUserDefinedCodesAsync` (`SY`, `RT`, `KY`, `DL01`, `DL02`)

## API Reference

### JdeClient Class

Main entry point for all JDE operations.

#### Connection Management

```csharp
// Connect to JDE
Task ConnectAsync(CancellationToken cancellationToken = default)

// Disconnect from JDE
Task DisconnectAsync()

// Check connection status
bool IsConnected { get; }
```

#### Object Catalog

```csharp
// Get objects from F9860
Task<List<JdeObjectInfo>> GetObjectsAsync(
    JdeObjectType? objectType = null,
    string? searchPattern = null,
    string? descriptionPattern = null,
    int? maxResults = null,
    CancellationToken cancellationToken = default)
```

**Object Types:**
- `JdeObjectType.All` - All objects
- `JdeObjectType.Application` - Interactive Applications (APPL)
- `JdeObjectType.BusinessFunctionLibrary` - Business Function Libraries (BL)
- `JdeObjectType.BusinessFunction` - Business Function Modules (BSFN)
- `JdeObjectType.BusinessView` - Business Views (BSVW)
- `JdeObjectType.DataStructure` - Data Structures (DSTR)
- `JdeObjectType.MediaObjectDataStructure` - Media Object Data Structures (GT)
- `JdeObjectType.Table` - Table Definitions (TBLE)
- `JdeObjectType.Report` - Batch Applications/UBEs (UBE)

#### Table Metadata

```csharp
// Get table information
Task<JdeTableInfo?> GetTableInfoAsync(
    string tableName,
    CancellationToken cancellationToken = default)
```

#### Table Query

```csharp
// Count rows
Task<int> QueryTableCountAsync(
    string tableName,
    IReadOnlyList<JdeFilter>? filters = null,
    string? dataSourceOverride = null,
    CancellationToken cancellationToken = default)

// Buffered query (small/medium result sets)
Task<JdeQueryResult> QueryTableAsync(
    string tableName,
    int maxRows = 1000,
    CancellationToken cancellationToken = default)

// Buffered query with filters + optional data source override
Task<JdeQueryResult> QueryTableAsync(
    string tableName,
    IReadOnlyList<JdeFilter>? filters,
    int maxRows = 1000,
    string? dataSourceOverride = null,
    CancellationToken cancellationToken = default)

// Streamed query (large result sets / incremental processing)
JdeQueryStream QueryTableStream(
    string tableName,
    IReadOnlyList<JdeFilter>? filters = null,
    IReadOnlyList<JdeSort>? sorts = null,
    int maxRows = 0,
    string? dataSourceOverride = null,
    int? indexId = null,
    bool allowDataSourceFallback = true,
    CancellationToken cancellationToken = default)

// Streamed business view query
JdeQueryStream QueryViewStream(
    string viewName,
    IReadOnlyList<JdeFilter>? filters = null,
    IReadOnlyList<JdeSort>? sorts = null,
    int maxRows = 0,
    string? dataSourceOverride = null,
    bool allowDataSourceFallback = true,
    CancellationToken cancellationToken = default)

// Project metadata (OMW)
Task<List<JdeProjectInfo>> GetProjectsAsync(
    string? status = null,
    string? dataSourceOverride = null,
    string? user = null,
    CancellationToken cancellationToken = default)

Task<List<JdeProjectObjectInfo>> GetProjectObjectsAsync(
    string projectName,
    string? pathCode = null,
    string? dataSourceOverride = null,
    CancellationToken cancellationToken = default)

Task<JdeOmwExportResult> ExportProjectToParAsync(
    string projectName,
    string pathCode,
    string? outputPath = null,
    bool insertOnly = false,
    int include64BitOption = 2,
    CancellationToken cancellationToken = default)

// User Defined Codes (UDC)
Task<List<JdeUserDefinedCodeTypes>> GetUserDefinedCodeTypesAsync(
    string? productCode = null,
    string? userDefinedCode = null,
    string? dataSourceOverride = null,
    int maxRows = 0,
    CancellationToken cancellationToken = default)

Task<List<JdeUserDefinedCodes>> GetUserDefinedCodesAsync(
    string productCode,
    string userDefinedCodeType,
    string? userDefinedCode,
    string? description,
    string? description2,
    string? dataSourceOverride = null,
    int maxRows = 0,
    CancellationToken cancellationToken = default)

// Event rules and specs
Task<JdeEventRulesNode> GetEventRulesTreeAsync(
    JdeObjectInfo jdeObject,
    CancellationToken cancellationToken = default)

Task<IReadOnlyList<JdeEventRulesXmlDocument>> GetEventRulesXmlAsync(
    string eventSpecKey,
    CancellationToken cancellationToken = default)

Task<IReadOnlyList<JdeSpecXmlDocument>> GetDataStructureXmlAsync(
    string templateName,
    CancellationToken cancellationToken = default)
```

### OMW export (solution/project)

`ExportProjectToParAsync` mirrors the OMW "Save" export behavior and writes a `.par` archive for the
requested solution/project.

```csharp
using var client = new JdeClient();
await client.ConnectAsync();

// outputPath can be a directory or a full .par file path
var result = await client.ExportProjectToParAsync(
    projectName: "2026-ABC-001",
    pathCode: "DV920",
    outputPath: @"C:\temp");

Console.WriteLine(result.OutputPath);
```

Notes:
- `outputPath` can be a directory or a `.par` file name. Directories are expanded to
  `PRJ_{ProjectName}_60_99.par`.
- OMW export requires `jdeomw.dll` (shipped with Fat Client) and an active fat client session.

### Models

#### JdeObjectInfo

```csharp
public class JdeObjectInfo
{
    public string ObjectName { get; set; }      // e.g., "F0101"
    public string ObjectType { get; set; }      // e.g., "TBLE"
    public string? Description { get; set; }
    public string? SystemCode { get; set; }     // e.g., "01"
    public string? ProductCode { get; set; }
}
```

#### JdeTableInfo

```csharp
public class JdeTableInfo
{
    public string TableName { get; set; }       // e.g., "F0101"
    public string? Description { get; set; }
    public List<JdeColumn> Columns { get; set; }
    public string? SystemCode { get; set; }
}
```

#### JdeColumn

```csharp
public class JdeColumn
{
    public string Name { get; set; }            // e.g., "ABAN8"
    public int DataType { get; set; }
    public int Length { get; set; }
    public int Decimals { get; set; }
    public string? Description { get; set; }
}
```

### Exceptions

All exceptions inherit from `JdeException`:

- `JdeConnectionException` - Connection failures
- `JdeApiException` - API call failures
- `JdeTableException` - Table operation failures

```csharp
try
{
    await client.ConnectAsync();
}
catch (JdeConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    Console.WriteLine($"Result code: {ex.ResultCode}");
}
```

#### JdeProjectInfo

```csharp
public class JdeProjectInfo
{
    public string ProjectName { get; set; }    // OMWPRJID
    public string? Description { get; set; }   // OMWDESC
    public string? Status { get; set; }        // OMWPS
}
```

#### JdeProjectObjectInfo

```csharp
public class JdeProjectObjectInfo
{
    public string ProjectName { get; set; }    // OMWPRJID
    public string ObjectId { get; set; }       // OMWOBJID
    public string ObjectName { get; set; }     // parsed from OMWOBJID
    public string? VersionName { get; set; }
    public string ObjectType { get; set; }     // OMWOT
    public string? PathCode { get; set; }      // PATHCD
}
```

#### JdeOmwExportResult

```csharp
public class JdeOmwExportResult
{
    public string ObjectId { get; set; }       // exported object id
    public string ObjectType { get; set; }     // e.g., "PRJ"
    public string ProjectName { get; set; }    // project name used for export
    public string? OutputPath { get; set; }    // resolved output path when provided
    public bool FileAlreadyExists { get; set; }
}
```

## Architecture

### Layer Separation

```
Public API (JdeClient.cs)
    ->
Internal (JdeSession.cs, query engines)
    ->
P/Invoke Interop (JdeKernelApi.cs, JdbrsApi.cs)
    ->
jdekrnl.dll (JDE Native APIs)
```

**Public Surface:**
- `JdeClient` - Main API
- `JdeObjectInfo`, `JdeTableInfo`, etc. - Models
- `JdeException` and subclasses - Exceptions

**Internal (not exposed):**
- P/Invoke declarations
- Native structures (HENV, HUSER, HREQUEST)
- Memory management details
- Marshaling logic

For a mapping of these workflows to the underlying JDE C APIs, see
`docs/JDE_API_WORKFLOWS.md`.

### Design Principles

1. **Clean API** - Hide all P/Invoke complexity
2. **Memory Safe** - Proper dispose pattern, no leaks
3. **Async First** - All I/O operations are async
4. **Exception Safe** - Native errors converted to exceptions
5. **Testable** - Internal interfaces for mocking (future)

## Requirements

### Runtime Requirements

- .NET 8
- Windows OS (JDE Fat Client is Windows-only)
- JDE EnterpriseOne Fat Client installed
- `activConsole.exe` must be running and user logged in
- JDE runtime available (`jdekrnl.dll` ships with the fat client; your host process must be able to resolve it)
- OMW export requires `jdeomw.dll` (also shipped with the fat client)

### Development Requirements

- .NET 8 SDK
- Visual Studio 2022, Rider, or any other IDE
- Optional: JDE installation for testing

## Logging

JdeClient.Core emits debug output only when enabled via `JdeClientOptions`.
You can route logs to a custom sink by setting `LogSink`.

```csharp
var options = new JdeClientOptions
{
    EnableDebug = true,
    EnableSpecDebug = true,
    EnableQueryDebug = true,
    LogSink = message => /* write to your logger or file */
};

using var client = new JdeClient(options);
```

Spec Lens (the Avalonia app) exposes logging controls in Settings and writes
app and client logs to separate files. See `../docs/LOGGING.md` for details.

## Troubleshooting

### "jdekrnl.dll not found"

**Solution:**
- Ensure JDE Fat Client is installed
- Ensure the JDE runtime directory is in the process DLL search path
  (SpecLens/TestConsole prefer the `activConsole.exe` directory automatically)
- Example JDE runtime path:
  ```
  C:\\E920_1\\system\\bin64
  ```

### "activConsole.exe not running"

**Solution:**
- Start JDE Fat Client
- Log in with valid credentials
- Then run your application

### Connection fails with "JDB_InitEnv failed"

**Possible causes:**
- JDE not properly installed
- Wrong DLL architecture (x86 vs x64)
- JDE configuration issue

**Solution:**
- Verify JDE installation
- Check JDE INI files
- Try running as Administrator

## Performance Considerations

- **Connection pooling**: Not implemented - create one client per session
- **Caching**: Spec data is cached by JDE APIs internally
- **Memory**: Always dispose JdeClient to free handles
- **Threading**: Client is NOT thread-safe - use one per thread

## Contributing

Contributions are welcome via pull requests. See the root CONTRIBUTING.md.

## License

Licensed under the GNU GPLv3. See LICENSE in the repository root.

**Built with .NET 8 and modern C# practices**



