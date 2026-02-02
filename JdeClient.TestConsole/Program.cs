using JdeClient.Core;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Models;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace JdeClient.TestConsole;

/// <summary>
/// Interactive test console for JdeClient.Core library
/// </summary>
class Program
{
    private static List<JdeObjectInfo>? cachedTables;
    private static List<JdeObjectInfo> lastSearchResults = new();
    private static JdeObjectInfo? selectedTable;
    private static List<JdeObjectInfo>? cachedViews;
    private static List<JdeObjectInfo> lastViewSearchResults = new();
    private static JdeObjectInfo? selectedView;
    private static List<(string Path, JdeEventRulesNode Node)> lastEventRulesNodes = new();

    static async Task<int> Main(string[] args)
    {
        bool debugEnabled = PromptDebugLogging();
        var options = new JdeClientOptions
        {
            EnableDebug = debugEnabled,
            EnableSpecDebug = debugEnabled,
            EnableQueryDebug = debugEnabled
        };
        Console.WriteLine("=================================================");
        Console.WriteLine("   JdeClient.Core - Interactive Test Console");
        Console.WriteLine("=================================================");
        Console.WriteLine();
        Console.WriteLine($"Debug logging: {(debugEnabled ? "enabled" : "disabled")}");
        Console.WriteLine();

        PreferJdeRuntime();
        LogRuntimeInfo();

        // Check if JDE Fat Client is running
        if (!IsJdeFatClientRunning())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: JDE Fat Client (activConsole.exe) is not running!");
            Console.ResetColor();
            Console.WriteLine("Please start JDE and log in, then run this test again.");
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ JDE Fat Client detected");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            using var client = new JdeClient.Core.JdeClient(options);

            // Run all tests
            await TestConnection(client);
            await TestWpfWorkflow(client);
            await TestBusinessViewWorkflow(client);

            // Interactive menu
            await InteractiveMenu(client);

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nFATAL ERROR: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine($"Details: {ex}");
            return 1;
        }
    }

    static bool PromptDebugLogging()
    {
        Console.Write("Enable debug logging? (y/N): ");
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return input.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsJdeFatClientRunning()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("activConsole");
        return processes.Length > 0;
    }

    static void PreferJdeRuntime()
    {
        string? activConsolePath = TryGetActivConsolePath();
        if (string.IsNullOrWhiteSpace(activConsolePath))
        {
            return;
        }

        string? activConsoleDir = Path.GetDirectoryName(activConsolePath);
        if (string.IsNullOrWhiteSpace(activConsoleDir))
        {
            return;
        }

        SetDllDirectoryW(activConsoleDir);

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Contains(activConsoleDir, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("PATH", $"{activConsoleDir};{path}");
        }
    }

    static void LogRuntimeInfo()
    {
        Console.WriteLine("Runtime info:");
        Console.WriteLine($"  Process bitness: {(Environment.Is64BitProcess ? "x64" : "x86")}");
        Console.WriteLine($"  OS bitness: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string? pathHint = path
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(p => p.Contains("JDE", StringComparison.OrdinalIgnoreCase) ||
                                 p.Contains("E920", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(pathHint))
        {
            Console.WriteLine($"  PATH hint: {pathHint}");
        }

        string? activConsolePath = TryGetActivConsolePath();
        if (!string.IsNullOrWhiteSpace(activConsolePath))
        {
            Console.WriteLine($"  activConsole.exe: {activConsolePath}");
        }

        if (NativeLibrary.TryLoad("jdekrnl.dll", out IntPtr handle))
        {
            Console.WriteLine($"  jdekrnl.dll: {GetModuleFilePath(handle)}");
        }
        else
        {
            Console.WriteLine("  jdekrnl.dll: not found on PATH");
        }

        Console.WriteLine();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetModuleFileNameW(IntPtr hModule, StringBuilder lpFilename, uint nSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectoryW(string lpPathName);

    static string GetModuleFilePath(IntPtr moduleHandle)
    {
        var buffer = new StringBuilder(260);
        uint result = GetModuleFileNameW(moduleHandle, buffer, (uint)buffer.Capacity);
        return result == 0 ? "<unknown>" : buffer.ToString();
    }

    static string? TryGetActivConsolePath()
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessesByName("activConsole")
                .FirstOrDefault();
            if (process == null)
            {
                return null;
            }

            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    static async Task TestConnection(JdeClient.Core.JdeClient client)
    {
        Console.WriteLine("TEST 1: Connection Management");
        Console.WriteLine("==============================");

        try
        {
            Console.Write("Connecting to JDE... ");
            await client.ConnectAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SUCCESS");
            Console.ResetColor();
            Console.WriteLine($"  Connection Status: {client.IsConnected}");
        }
        catch (JdeConnectionException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
            throw;
        }

        Console.WriteLine();
    }

    static async Task TestWpfWorkflow(JdeClient.Core.JdeClient client)
    {
        Console.WriteLine("TEST 2: WPF Workflow Smoke Test");
        Console.WriteLine("===============================");

        Console.Write("Loading table catalog (F9860)... ");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tables = await LoadTableCatalogAsync(client, forceReload: true);
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SUCCESS ({tables.Count} tables, {sw.ElapsedMilliseconds}ms)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine();
            return;
        }

        Console.WriteLine();

        Console.Write("Searching for table F0101 (exact match)... ");
        try
        {
            var tables = cachedTables ?? new List<JdeObjectInfo>();
            var exact = tables.FirstOrDefault(t => string.Equals(t.ObjectName, "F0101", StringComparison.OrdinalIgnoreCase));
            if (exact == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("NOT FOUND");
                Console.ResetColor();
                exact = tables.FirstOrDefault();
                if (exact != null)
                {
                    Console.WriteLine($"  Using fallback table: {exact.ObjectName}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("FOUND");
                Console.ResetColor();
            }

            selectedTable = exact;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();

        if (selectedTable == null)
        {
            Console.WriteLine("Skipping specs/query checks because no table was selected.");
            Console.WriteLine();
            return;
        }

        await TestTableSpecs(client, selectedTable.ObjectName);
        await TestTableQueryStream(client, selectedTable.ObjectName);
    }

    static async Task TestBusinessViewWorkflow(JdeClient.Core.JdeClient client)
    {
        Console.WriteLine("TEST 3: Business View Workflow Smoke Test");
        Console.WriteLine("=========================================");

        Console.Write("Loading business view catalog (F9860)... ");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var views = await LoadViewCatalogAsync(client, forceReload: true);
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SUCCESS ({views.Count} views, {sw.ElapsedMilliseconds}ms)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine();
            return;
        }

        Console.WriteLine();

        Console.Write("Searching for view V0101 (exact match)... ");
        try
        {
            var views = cachedViews ?? new List<JdeObjectInfo>();
            var exact = views.FirstOrDefault(v => string.Equals(v.ObjectName, "V0101", StringComparison.OrdinalIgnoreCase))
                        ?? views.FirstOrDefault(v => string.Equals(v.ObjectName, "V0101A", StringComparison.OrdinalIgnoreCase));
            if (exact == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("NOT FOUND");
                Console.ResetColor();
                exact = views.FirstOrDefault();
                if (exact != null)
                {
                    Console.WriteLine($"  Using fallback view: {exact.ObjectName}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("FOUND");
                Console.ResetColor();
            }

            selectedView = exact;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();

        if (selectedView == null)
        {
            Console.WriteLine("Skipping view specs/query checks because no view was selected.");
            Console.WriteLine();
            return;
        }

        await TestBusinessViewSpecs(client, selectedView.ObjectName);
        await TestBusinessViewQueryStream(client, selectedView.ObjectName);
    }

    static async Task TestTableSpecs(JdeClient.Core.JdeClient client, string tableName)
    {
        Console.Write($"Loading specs for {tableName}... ");
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            var tableInfo = await client.GetTableInfoAsync(tableName);
            Console.WriteLine(tableInfo == null ? "NOT FOUND" : "SUCCESS");
            Console.ResetColor();

            if (tableInfo != null)
            {
                Console.WriteLine($"  Columns: {tableInfo.Columns.Count}");
                Console.WriteLine($"  First columns: {string.Join(", ", tableInfo.Columns.Select(c => c.Name).Take(8))}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    static async Task TestBusinessViewSpecs(JdeClient.Core.JdeClient client, string viewName)
    {
        Console.Write($"Loading specs for {viewName}... ");
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            var viewInfo = await client.GetBusinessViewInfoAsync(viewName);
            Console.WriteLine(viewInfo == null ? "NOT FOUND" : "SUCCESS");
            Console.ResetColor();

            if (viewInfo != null)
            {
                Console.WriteLine($"  Tables: {viewInfo.Tables.Count}");
                Console.WriteLine($"  Columns: {viewInfo.Columns.Count}");
                Console.WriteLine($"  Joins: {viewInfo.Joins.Count}");
                Console.WriteLine($"  First columns: {string.Join(", ", viewInfo.Columns.Select(c => c.DataItem).Take(8))}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    static Task TestTableQueryStream(JdeClient.Core.JdeClient client, string tableName)
    {
        const int maxRows = 25;
        Console.Write($"Streaming {tableName} (max {maxRows} rows)... ");
        try
        {
            var stream = client.QueryTableStream(tableName, maxRows);
            int rowCount = 0;
            List<Dictionary<string, object>> previewRows = new();

            foreach (var row in stream)
            {
                rowCount++;
                if (previewRows.Count < 3)
                {
                    previewRows.Add(row);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SUCCESS ({rowCount} rows)");
            Console.ResetColor();

            Console.WriteLine($"  Columns: {stream.ColumnNames.Count}");
            Console.WriteLine($"  First columns: {string.Join(", ", stream.ColumnNames.Take(8))}");

            for (int i = 0; i < previewRows.Count; i++)
            {
                var row = previewRows[i];
                var preview = stream.ColumnNames.Take(6)
                    .Select(c => $"{c}={row.GetValueOrDefault(c, "")}");
                Console.WriteLine($"  Row {i + 1}: {string.Join(", ", preview)}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();
        return Task.CompletedTask;
    }

    static Task TestBusinessViewQueryStream(JdeClient.Core.JdeClient client, string viewName)
    {
        const int maxRows = 25;
        Console.Write($"Streaming {viewName} (max {maxRows} rows)... ");
        try
        {
            var stream = client.QueryViewStream(
                viewName,
                filters: Array.Empty<JdeFilter>(),
                sorts: null,
                maxRows: maxRows,
                dataSourceOverride: null,
                allowDataSourceFallback: true,
                cancellationToken: CancellationToken.None);
            int rowCount = 0;
            List<Dictionary<string, object>> previewRows = new();

            foreach (var row in stream)
            {
                rowCount++;
                if (previewRows.Count < 3)
                {
                    previewRows.Add(row);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SUCCESS ({rowCount} rows)");
            Console.ResetColor();

            Console.WriteLine($"  Columns: {stream.ColumnNames.Count}");
            Console.WriteLine($"  First columns: {string.Join(", ", stream.ColumnNames.Take(8))}");

            for (int i = 0; i < previewRows.Count; i++)
            {
                var row = previewRows[i];
                var preview = stream.ColumnNames.Take(6)
                    .Select(c => $"{c}={row.GetValueOrDefault(c, "")}");
                Console.WriteLine($"  Row {i + 1}: {string.Join(", ", preview)}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();
        return Task.CompletedTask;
    }

    static async Task InteractiveMenu(JdeClient.Core.JdeClient client)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=================================================");
            Console.WriteLine("Interactive Menu");
            Console.WriteLine("=================================================");
            Console.WriteLine("1. Run workflow smoke test");
            Console.WriteLine("2. Search for tables");
            Console.WriteLine("3. Select table from last search");
            Console.WriteLine("4. Open table specs");
            Console.WriteLine("5. Open query (load columns)");
            Console.WriteLine("6. Run query (stream)");
            Console.WriteLine("7. Reload table catalog");
            Console.WriteLine("8. Search for business views");
            Console.WriteLine("9. Select business view from last search");
            Console.WriteLine("10. Open view specs");
            Console.WriteLine("11. Open view query (load columns)");
            Console.WriteLine("12. Run view query (stream)");
            Console.WriteLine("13. Dump data dictionary details");
            Console.WriteLine("14. Load event rules tree");
            Console.WriteLine("15. Load event rules lines");
            Console.WriteLine("16. Disconnect and exit");
            Console.WriteLine();
            Console.Write("Select option (1-16): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestWpfWorkflow(client);
                    await TestBusinessViewWorkflow(client);
                    break;
                case "2":
                    await SearchTables(client, forceReload: false);
                    break;
                case "3":
                    SelectTableFromResults();
                    break;
                case "4":
                    await OpenTableSpecs(client);
                    break;
                case "5":
                    await OpenQueryColumns(client);
                    break;
                case "6":
                    await RunQueryStream(client);
                    break;
                case "7":
                    await SearchTables(client, forceReload: true);
                    break;
                case "8":
                    await SearchViews(client, forceReload: false);
                    break;
                case "9":
                    SelectViewFromResults();
                    break;
                case "10":
                    await OpenViewSpecs(client);
                    break;
                case "11":
                    await OpenViewColumns(client);
                    break;
                case "12":
                    await RunViewQueryStream(client);
                    break;
                case "13":
                    await DumpDataDictionaryDetails(client);
                    break;
                case "14":
                    await LoadEventRulesTree(client);
                    break;
                case "15":
                    await LoadEventRulesLines(client);
                    break;
                case "16":
                    Console.WriteLine("\nDisconnecting...");
                    await client.DisconnectAsync();
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    static async Task SearchTables(JdeClient.Core.JdeClient client, bool forceReload)
    {
        Console.Write("\nEnter table name pattern (wildcards with * supported): ");
        var pattern = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.WriteLine($"\nSearching for tables matching '{pattern}'...");
        List<JdeObjectInfo> matches;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            matches = await LoadTableCatalogAsync(client, forceReload);
        }
        else
        {
            matches = await client.GetObjectsAsync(JdeObjectType.Table, searchPattern: pattern, maxResults: 50000);
        }

        Console.WriteLine($"Found {matches.Count} matches:");
        for (int i = 0; i < matches.Take(20).Count(); i++)
        {
            var table = matches[i];
            Console.WriteLine($"  {i + 1,3}. {table.ObjectName,-10} - {table.Description}");
        }
        if (matches.Count > 20)
        {
            Console.WriteLine($"  ... and {matches.Count - 20} more");
        }

        lastSearchResults = matches;
        if (matches.Count == 1)
        {
            selectedTable = matches[0];
            Console.WriteLine($"Selected {selectedTable.ObjectName}");
        }
    }

    static async Task SearchViews(JdeClient.Core.JdeClient client, bool forceReload)
    {
        Console.Write("\nEnter view name pattern (wildcards with * supported): ");
        var pattern = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.WriteLine($"\nSearching for views matching '{pattern}'...");
        List<JdeObjectInfo> matches;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            matches = await LoadViewCatalogAsync(client, forceReload);
        }
        else
        {
            matches = await client.GetObjectsAsync(JdeObjectType.BusinessView, searchPattern: pattern, maxResults: 50000);
        }

        Console.WriteLine($"Found {matches.Count} matches:");
        for (int i = 0; i < matches.Take(20).Count(); i++)
        {
            var view = matches[i];
            Console.WriteLine($"  {i + 1,3}. {view.ObjectName,-10} - {view.Description}");
        }
        if (matches.Count > 20)
        {
            Console.WriteLine($"  ... and {matches.Count - 20} more");
        }

        lastViewSearchResults = matches;
        if (matches.Count == 1)
        {
            selectedView = matches[0];
            Console.WriteLine($"Selected {selectedView.ObjectName}");
        }
    }

    static void SelectTableFromResults()
    {
        if (lastSearchResults.Count == 0)
        {
            Console.WriteLine("No search results available. Run a search first.");
            return;
        }

        Console.Write("\nEnter result number to select: ");
        if (!int.TryParse(Console.ReadLine(), out int index))
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        if (index < 1 || index > lastSearchResults.Count)
        {
            Console.WriteLine("Selection out of range.");
            return;
        }

        selectedTable = lastSearchResults[index - 1];
        Console.WriteLine($"Selected {selectedTable.ObjectName}");
    }

    static void SelectViewFromResults()
    {
        if (lastViewSearchResults.Count == 0)
        {
            Console.WriteLine("No view search results available. Run a search first.");
            return;
        }

        Console.Write("\nEnter result number to select: ");
        if (!int.TryParse(Console.ReadLine(), out int index))
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        if (index < 1 || index > lastViewSearchResults.Count)
        {
            Console.WriteLine("Selection out of range.");
            return;
        }

        selectedView = lastViewSearchResults[index - 1];
        Console.WriteLine($"Selected {selectedView.ObjectName}");
    }

    static async Task OpenTableSpecs(JdeClient.Core.JdeClient client)
    {
        string tableName = ResolveSelectedTableName();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        Console.WriteLine($"\nRetrieving specs for {tableName}...");
        var tableInfo = await client.GetTableInfoAsync(tableName);

        if (tableInfo != null)
        {
            Console.WriteLine($"\nTable: {tableInfo.TableName}");
            Console.WriteLine($"Description: {tableInfo.Description ?? "N/A"}");
            Console.WriteLine($"System Code: {tableInfo.SystemCode ?? "N/A"}");
            Console.WriteLine($"Columns: {tableInfo.Columns.Count}");

            if (tableInfo.Columns.Any())
            {
                Console.WriteLine("\nColumns:");
                foreach (var col in tableInfo.Columns.Take(10))
                {
                    Console.WriteLine($"  {col.Name,-15} Type: {col.DataType}, Length: {col.Length}");
                }
                if (tableInfo.Columns.Count > 10)
                {
                    Console.WriteLine($"  ... and {tableInfo.Columns.Count - 10} more columns");
                }
            }
        }
        else
        {
            Console.WriteLine($"Table {tableName} not found.");
        }
    }

    static async Task OpenViewSpecs(JdeClient.Core.JdeClient client)
    {
        string viewName = ResolveSelectedViewName();
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return;
        }

        Console.WriteLine($"\nRetrieving specs for {viewName}...");
        var viewInfo = await client.GetBusinessViewInfoAsync(viewName);

        if (viewInfo != null)
        {
            Console.WriteLine($"\nView: {viewInfo.ViewName}");
            Console.WriteLine($"Description: {viewInfo.Description ?? "N/A"}");
            Console.WriteLine($"System Code: {viewInfo.SystemCode ?? "N/A"}");
            Console.WriteLine($"Tables: {viewInfo.Tables.Count}");
            Console.WriteLine($"Columns: {viewInfo.Columns.Count}");
            Console.WriteLine($"Joins: {viewInfo.Joins.Count}");

            if (viewInfo.Columns.Any())
            {
                Console.WriteLine("\nColumns:");
                foreach (var col in viewInfo.Columns.Take(10))
                {
                    Console.WriteLine($"  {col.DataItem,-15} Type: {col.DataType}, Length: {col.Length}, Table: {col.TableName}");
                }
                if (viewInfo.Columns.Count > 10)
                {
                    Console.WriteLine($"  ... and {viewInfo.Columns.Count - 10} more columns");
                }
            }
        }
        else
        {
            Console.WriteLine($"View {viewName} not found.");
        }
    }

    static async Task DumpDataDictionaryDetails(JdeClient.Core.JdeClient client)
    {
        Console.Write("\nEnter data dictionary item (DTAI) to inspect: ");
        var item = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(item))
        {
            Console.WriteLine("No data item provided.");
            return;
        }

        Console.WriteLine($"\nRetrieving data dictionary details for {item}...");
        var detailsList = await client.GetDataDictionaryDetailsAsync(new[] { item });
        if (detailsList.Count == 0)
        {
            Console.WriteLine("No data dictionary data found.");
            return;
        }

        foreach (var details in detailsList)
        {
            Console.WriteLine();
            Console.WriteLine($"Data Item: {details.DataItem}");
            Console.WriteLine($"Dictionary Name: {details.DictionaryName}");
            Console.WriteLine($"Alias: {details.Alias}");
            Console.WriteLine($"System Code: {details.SystemCode}");
            Console.WriteLine($"Glossary Group: {details.GlossaryGroup}");
            Console.WriteLine($"Error Level: {details.ErrorLevel}");
            Console.WriteLine($"Type Code: {details.TypeCode}");
            Console.WriteLine($"Everest Type: {details.EverestType}");
            Console.WriteLine($"AS/400 Class: {details.As400Class}");
            Console.WriteLine($"Length: {details.Length}");
            Console.WriteLine($"Decimals: {details.Decimals}");
            Console.WriteLine($"Display Decimals: {details.DisplayDecimals}");
            Console.WriteLine($"Default Value: {details.DefaultValue}");
            Console.WriteLine($"Control Type: {details.ControlType}");
            Console.WriteLine($"AS/400 Edit Rule: {details.As400EditRule}");
            Console.WriteLine($"AS/400 Edit Parm1: {details.As400EditParm1}");
            Console.WriteLine($"AS/400 Edit Parm2: {details.As400EditParm2}");
            Console.WriteLine($"AS/400 Disp Rule: {details.As400DispRule}");
            Console.WriteLine($"AS/400 Disp Parm: {details.As400DispParm}");
            Console.WriteLine($"Edit Behavior: {details.EditBehavior}");
            Console.WriteLine($"Display Behavior: {details.DisplayBehavior}");
            Console.WriteLine($"Security Flag: {details.SecurityFlag}");
            Console.WriteLine($"Next Number Index: {details.NextNumberIndex}");
            Console.WriteLine($"Next Number System: {details.NextNumberSystem}");
            Console.WriteLine($"Style: {details.Style}");
            Console.WriteLine($"Behavior: {details.Behavior}");
            Console.WriteLine($"DS Template Name: {details.DataSourceTemplateName}");
            Console.WriteLine($"Display Rule BF Name: {details.DisplayRuleBfnName}");
            Console.WriteLine($"Edit Rule BF Name: {details.EditRuleBfnName}");
            Console.WriteLine($"Search Form Name: {details.SearchFormName}");
            Console.WriteLine($"Var Length: {details.VarLength}");
            Console.WriteLine($"Format Number: {details.FormatNumber}");

            if (details.Texts.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("DDTEXT Entries:");
                foreach (var text in details.Texts.OrderBy(t => t.TextType))
                {
                    Console.WriteLine($"  {GetTextTypeLabel(text.TextType)} ({text.TextType})");
                    Console.WriteLine($"    Text: {text.Text}");
                    Console.WriteLine($"    Language: {text.Language}");
                    Console.WriteLine($"    System Code: {text.SystemCode}");
                    Console.WriteLine($"    Dictionary Name: {text.DictionaryName}");
                    Console.WriteLine($"    Var Length: {text.VarLength}");
                    Console.WriteLine($"    Format Number: {text.FormatNumber}");
                }
            }
        }
    }

    static string GetTextTypeLabel(char textType)
    {
        return textType switch
        {
            'A' => "Alpha Description",
            'R' => "Row Description",
            'C' => "Column Title",
            'H' => "Glossary",
            _ => "Text"
        };
    }

    static async Task OpenQueryColumns(JdeClient.Core.JdeClient client)
    {
        string tableName = ResolveSelectedTableName();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        Console.WriteLine($"\nLoading columns for {tableName}...");
        var tableInfo = await client.GetTableInfoAsync(tableName);
        if (tableInfo == null)
        {
            Console.WriteLine("Table not found.");
            return;
        }

        Console.WriteLine($"Columns: {tableInfo.Columns.Count}");
        Console.WriteLine($"First columns: {string.Join(", ", tableInfo.Columns.Select(c => c.Name).Take(12))}");
    }

    static async Task OpenViewColumns(JdeClient.Core.JdeClient client)
    {
        string viewName = ResolveSelectedViewName();
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return;
        }

        Console.WriteLine($"\nLoading columns for {viewName}...");
        var viewInfo = await client.GetBusinessViewInfoAsync(viewName);
        if (viewInfo == null)
        {
            Console.WriteLine("View not found.");
            return;
        }

        Console.WriteLine($"Columns: {viewInfo.Columns.Count}");
        Console.WriteLine($"First columns: {string.Join(", ", viewInfo.Columns.Select(c => string.IsNullOrWhiteSpace(c.TableName) ? c.DataItem : $"{c.TableName}.{c.DataItem}").Take(12))}");
    }

    static Task RunQueryStream(JdeClient.Core.JdeClient client)
    {
        string tableName = ResolveSelectedTableName();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return Task.CompletedTask;
        }

        var filters = PromptFilters();
        Console.Write("\nMax rows (blank for 100): ");
        string? maxInput = Console.ReadLine();
        int maxRows = 100;
        if (!string.IsNullOrWhiteSpace(maxInput) && int.TryParse(maxInput, out int parsed))
        {
            maxRows = parsed;
        }

        Console.WriteLine("\nStreaming rows (press Q to stop)...");
        using var cts = new CancellationTokenSource();
        var stream = client.QueryTableStream(
            tableName,
            filters: filters,
            sorts: null,
            maxRows: maxRows,
            dataSourceOverride: null,
            indexId: null,
            allowDataSourceFallback: true,
            cancellationToken: cts.Token);
        int rowCount = 0;
        foreach (var row in stream)
        {
            rowCount++;
            if (rowCount <= 3)
            {
                var preview = stream.ColumnNames.Take(6)
                    .Select(c => $"{c}={row.GetValueOrDefault(c, "")}");
                Console.WriteLine($"  Row {rowCount}: {string.Join(", ", preview)}");
            }

            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
            {
                cts.Cancel();
                break;
            }
        }

        Console.WriteLine($"Streamed {rowCount} rows.");
        return Task.CompletedTask;
    }

    static Task RunViewQueryStream(JdeClient.Core.JdeClient client)
    {
        string viewName = ResolveSelectedViewName();
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return Task.CompletedTask;
        }

        var filters = PromptFilters();
        Console.Write("\nMax rows (blank for 100): ");
        string? maxInput = Console.ReadLine();
        int maxRows = 100;
        if (!string.IsNullOrWhiteSpace(maxInput) && int.TryParse(maxInput, out int parsed))
        {
            maxRows = parsed;
        }

        Console.WriteLine("\nStreaming rows (press Q to stop)...");
        using var cts = new CancellationTokenSource();
        var stream = client.QueryViewStream(
            viewName,
            filters: filters,
            sorts: null,
            maxRows: maxRows,
            dataSourceOverride: null,
            allowDataSourceFallback: true,
            cancellationToken: cts.Token);
        int rowCount = 0;
        foreach (var row in stream)
        {
            rowCount++;
            if (rowCount <= 3)
            {
                var preview = stream.ColumnNames.Take(6)
                    .Select(c => $"{c}={row.GetValueOrDefault(c, "")}");
                Console.WriteLine($"  Row {rowCount}: {string.Join(", ", preview)}");
            }

            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
            {
                cts.Cancel();
                break;
            }
        }

        Console.WriteLine($"Streamed {rowCount} rows.");
        return Task.CompletedTask;
    }

    static async Task<List<JdeObjectInfo>> LoadTableCatalogAsync(JdeClient.Core.JdeClient client, bool forceReload)
    {
        if (!forceReload && cachedTables != null && cachedTables.Count > 0)
        {
            return cachedTables;
        }

        var tables = await client.GetObjectsAsync(JdeObjectType.Table, maxResults: 50000);
        cachedTables = tables;
        return tables;
    }

    static async Task<List<JdeObjectInfo>> LoadViewCatalogAsync(JdeClient.Core.JdeClient client, bool forceReload)
    {
        if (!forceReload && cachedViews != null && cachedViews.Count > 0)
        {
            return cachedViews;
        }

        var views = await client.GetObjectsAsync(JdeObjectType.BusinessView, maxResults: 50000);
        cachedViews = views;
        return views;
    }

    static string ResolveSelectedTableName()
    {
        if (selectedTable != null)
        {
            return selectedTable.ObjectName;
        }

        Console.Write("\nEnter table name: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    static string ResolveSelectedViewName()
    {
        if (selectedView != null)
        {
            return selectedView.ObjectName;
        }

        Console.Write("\nEnter business view name: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    static List<JdeFilter> PromptFilters()
    {
        Console.Write("Enter filters (COLUMN=VALUE, use TABLE.COLUMN for views, comma-separated) or blank: ");
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<JdeFilter>();
        }

        var filters = new List<JdeFilter>();
        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (!TryParseFilterExpression(trimmed, out var column, out var op, out var value))
            {
                continue;
            }

            filters.Add(new JdeFilter
            {
                ColumnName = column,
                Value = value,
                Operator = op
            });
        }

        return filters;
    }

    static bool TryParseFilterExpression(string expression, out string column, out JdeFilterOperator op, out string value)
    {
        column = string.Empty;
        value = string.Empty;
        op = JdeFilterOperator.Equals;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        string[] operators = { ">=", "<=", "!=", ">", "<", "=" };
        foreach (var token in operators)
        {
            int index = expression.IndexOf(token, StringComparison.Ordinal);
            if (index <= 0 || index >= expression.Length - token.Length)
            {
                continue;
            }

            column = expression.Substring(0, index).Trim();
            value = expression.Substring(index + token.Length).Trim();
            if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            op = token switch
            {
                ">=" => JdeFilterOperator.GreaterThanOrEqual,
                "<=" => JdeFilterOperator.LessThanOrEqual,
                "!=" => JdeFilterOperator.NotEquals,
                ">" => JdeFilterOperator.GreaterThan,
                "<" => JdeFilterOperator.LessThan,
                _ => JdeFilterOperator.Equals
            };

            if (op == JdeFilterOperator.Equals && value.Contains('*', StringComparison.Ordinal))
            {
                op = JdeFilterOperator.Like;
                value = value.Replace('*', '%');
            }

            return true;
        }

        return false;
    }

    static async Task LoadEventRulesTree(JdeClient.Core.JdeClient client)
    {
        var jdeObject = PromptEventRulesObject();
        if (jdeObject == null)
        {
            return;
        }

        Console.WriteLine($"\nLoading event rules for {jdeObject.ObjectName} ({jdeObject.ObjectType})...");
        try
        {
            var root = await client.GetEventRulesTreeAsync(jdeObject);
            var allNodes = new List<(string Path, JdeEventRulesNode Node)>();
            FlattenEventRulesNodes(root, string.Empty, allNodes);

            var eventNodes = allNodes
                .Where(entry => entry.Node.HasEventRules)
                .ToList();

            lastEventRulesNodes = eventNodes;

            Console.WriteLine($"Loaded {allNodes.Count} nodes ({eventNodes.Count} with event rules).");
            for (int i = 0; i < eventNodes.Take(20).Count(); i++)
            {
                Console.WriteLine($"  {i + 1,3}. {eventNodes[i].Path}");
            }
            if (eventNodes.Count > 20)
            {
                Console.WriteLine($"  ... and {eventNodes.Count - 20} more");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.ResetColor();
        }
    }

    static async Task LoadEventRulesLines(JdeClient.Core.JdeClient client)
    {
        if (lastEventRulesNodes.Count == 0)
        {
            Console.WriteLine("No event rules loaded. Run 'Load event rules tree' first.");
            return;
        }

        Console.Write("\nEnter event rules node number to view: ");
        if (!int.TryParse(Console.ReadLine(), out int index))
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        if (index < 1 || index > lastEventRulesNodes.Count)
        {
            Console.WriteLine("Selection out of range.");
            return;
        }

        var entry = lastEventRulesNodes[index - 1];
        if (string.IsNullOrWhiteSpace(entry.Node.EventSpecKey))
        {
            Console.WriteLine("Selected node does not have event rules.");
            return;
        }

        Console.WriteLine($"\nLoading event rules for {entry.Path}...");
        try
        {
            var lines = await client.GetEventRulesLinesAsync(entry.Node.EventSpecKey);
            Console.WriteLine($"Loaded {lines.Count} lines.");
            foreach (var line in lines.Take(60))
            {
                string indent = new string(' ', line.IndentLevel * 2);
                Console.WriteLine($"{indent}{line.Text}");
            }
            if (lines.Count > 60)
            {
                Console.WriteLine($"... and {lines.Count - 60} more");
            }

            await PromptSaveEventRulesXmlAsync(client, entry.Path, entry.Node.EventSpecKey);
            if (!string.IsNullOrWhiteSpace(entry.Node.DataStructureName))
            {
                await PromptSaveDataStructureXmlAsync(client, entry.Path, entry.Node.DataStructureName);
            }

        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.ResetColor();
        }
    }
    static void PrintEventRulesDiagnostics(IReadOnlyList<JdeEventRulesDecodeDiagnostics> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            Console.WriteLine("No diagnostics available.");
            return;
        }

        foreach (var diagnostic in diagnostics.Take(5))
        {
            Console.WriteLine($"\nSequence {diagnostic.Sequence}:");
            Console.WriteLine($"  Blob size: {diagnostic.BlobSize} bytes");
            Console.WriteLine($"  Head: {diagnostic.HeadHex}");
            Console.WriteLine($"  Raw looks like GBRSPEC: {diagnostic.RawLooksLikeGbrSpec}");
            PrintUnpackAttempt("Raw LE", diagnostic.RawLittleEndian);
            PrintUnpackAttempt("Raw BE", diagnostic.RawBigEndian);
            PrintB733Attempt("Raw B733 LE", diagnostic.RawB733LittleEndian);
            PrintB733Attempt("Raw B733 BE", diagnostic.RawB733BigEndian);

            if (diagnostic.Uncompressed)
            {
                Console.WriteLine($"  Uncompressed size: {diagnostic.UncompressedSize} bytes");
                Console.WriteLine($"  Uncompressed looks like GBRSPEC: {diagnostic.UncompressedLooksLikeGbrSpec}");
                PrintUnpackAttempt("Uncompressed LE", diagnostic.UncompressedLittleEndian);
                PrintUnpackAttempt("Uncompressed BE", diagnostic.UncompressedBigEndian);
                PrintB733Attempt("Uncompressed B733 LE", diagnostic.UncompressedB733LittleEndian);
                PrintB733Attempt("Uncompressed B733 BE", diagnostic.UncompressedB733BigEndian);
            }
            else
            {
                Console.WriteLine("  Uncompressed: false");
            }
        }
    }

    static void PrintUnpackAttempt(string label, JdeEventRulesDecodeDiagnostics.UnpackAttempt attempt)
    {
        string error = string.IsNullOrWhiteSpace(attempt.Error) ? string.Empty : $" Error: {attempt.Error}";
        Console.WriteLine($"  {label}: Status={attempt.Status}, Len={attempt.UnpackedLength}, LooksLike={attempt.LooksLikeGbrSpec}{error}");
    }

    static void PrintB733Attempt(string label, JdeEventRulesDecodeDiagnostics.B733UnpackAttempt attempt)
    {
        string error = string.IsNullOrWhiteSpace(attempt.Error) ? string.Empty : $" Error: {attempt.Error}";
        Console.WriteLine($"  {label}: Status={attempt.Status}, Len={attempt.UnpackedLength}, LooksLike={attempt.LooksLikeGbrSpec}, CP={attempt.CodePage}, OS={attempt.OsType}{error}");
    }



    static JdeObjectInfo? PromptEventRulesObject()
    {
        Console.Write("\nEnter object type (APPL/UBE/BSFN/NER/TBLE): ");
        string type = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            Console.WriteLine("Object type is required.");
            return null;
        }

        Console.Write("Enter object name: ");
        string name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Object name is required.");
            return null;
        }

        return new JdeObjectInfo
        {
            ObjectName = name,
            ObjectType = type
        };
    }

    static void FlattenEventRulesNodes(JdeEventRulesNode node, string parentPath, List<(string Path, JdeEventRulesNode Node)> output)
    {
        string path = string.IsNullOrWhiteSpace(parentPath) ? node.Name : $"{parentPath} / {node.Name}";
        output.Add((path, node));

        foreach (var child in node.Children)
        {
            FlattenEventRulesNodes(child, path, output);
        }
    }

    static async Task PromptSaveEventRulesXmlAsync(JdeClient.Core.JdeClient client, string nodePath, string eventSpecKey)
    {
        Console.Write("\nSave XML to file(s)? (y/N): ");
        string? response = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(response) || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var documents = await client.GetEventRulesXmlAsync(eventSpecKey);
        if (documents.Count == 0)
        {
            Console.WriteLine("No XML documents available.");
            return;
        }

        Console.Write("Output folder (blank for current): ");
        string? folder = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Environment.CurrentDirectory;
        }

        Directory.CreateDirectory(folder);

        string fileName = BuildNodeXmlFileName(nodePath, eventSpecKey);
        string path = Path.Combine(folder, fileName);
        string combinedXml = CombineXmlDocuments(documents);
        File.WriteAllText(path, combinedXml, Encoding.UTF8);

        Console.WriteLine($"Saved XML to {path}");
    }

    static string BuildNodeXmlFileName(string nodePath, string eventSpecKey)
    {
        string baseName = string.IsNullOrWhiteSpace(nodePath)
            ? $"ER_{eventSpecKey}"
            : $"ER_{nodePath}";

        return $"{SanitizeFileName(TrimFileName(baseName, 120))}.xml";
    }

    static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "event_rules";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (char ch in name)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    static string TrimFileName(string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(name) || maxLength <= 0)
        {
            return name;
        }

        return name.Length <= maxLength ? name : name.Substring(0, maxLength);
    }

    static string CombineXmlDocuments(IReadOnlyList<JdeEventRulesXmlDocument> documents)
    {
        if (documents.Count == 0)
        {
            return string.Empty;
        }

        if (documents.Count == 1)
        {
            return documents[0].Xml ?? string.Empty;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < documents.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(documents[i].Xml);
        }

        return builder.ToString();
    }

    static async Task PromptSaveDataStructureXmlAsync(JdeClient.Core.JdeClient client, string nodePath, string templateName)
    {
        Console.Write($"\nSave DSTMPL XML for {templateName}? (y/N): ");
        string? response = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(response) || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var documents = await client.GetDataStructureXmlAsync(templateName);
        if (documents.Count == 0)
        {
            Console.WriteLine("No DSTMPL XML documents available.");
            return;
        }

        Console.Write("Output folder (blank for current): ");
        string? folder = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Environment.CurrentDirectory;
        }

        Directory.CreateDirectory(folder);

        string fileName = BuildDataStructureXmlFileName(nodePath, templateName);
        string path = Path.Combine(folder, fileName);
        string combinedXml = CombineSpecXmlDocuments(documents);
        File.WriteAllText(path, combinedXml, Encoding.UTF8);

        Console.WriteLine($"Saved DSTMPL XML to {path}");
    }

    static string BuildDataStructureXmlFileName(string nodePath, string templateName)
    {
        string baseName = string.IsNullOrWhiteSpace(nodePath)
            ? $"DSTMPL_{templateName}"
            : $"DSTMPL_{nodePath}_{templateName}";

        return $"{SanitizeFileName(TrimFileName(baseName, 120))}.xml";
    }

    static string CombineSpecXmlDocuments(IReadOnlyList<JdeSpecXmlDocument> documents)
    {
        if (documents.Count == 0)
        {
            return string.Empty;
        }

        if (documents.Count == 1)
        {
            return documents[0].Xml ?? string.Empty;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < documents.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(documents[i].Xml);
        }

        return builder.ToString();
    }

}

