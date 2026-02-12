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
    private static string? cachedTablesLocationKey;
    private static List<JdeObjectInfo> lastSearchResults = new();
    private static JdeObjectInfo? selectedTable;
    private static ObjectLocationSelection selectedTableLocation = ObjectLocationSelection.Local;
    private static ObjectLocationSelection lastTableSearchLocation = ObjectLocationSelection.Local;
    private static List<JdeObjectInfo>? cachedViews;
    private static string? cachedViewsLocationKey;
    private static List<JdeObjectInfo> lastViewSearchResults = new();
    private static JdeObjectInfo? selectedView;
    private static ObjectLocationSelection selectedViewLocation = ObjectLocationSelection.Local;
    private static ObjectLocationSelection lastViewSearchLocation = ObjectLocationSelection.Local;
    private static ObjectLocationSelection currentObjectLocation = ObjectLocationSelection.Local;
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

            if (!await PromptConnectAsync(client))
            {
                Console.WriteLine("Exiting without connecting.");
                return 1;
            }

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

    static async Task<bool> PromptConnectAsync(JdeClient.Core.JdeClient client)
    {
        Console.Write("Connect to JDE now? (Y/n): ");
        string? input = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(input) &&
            input.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Console.Write("Connecting to JDE... ");
        try
        {
            await client.ConnectAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SUCCESS");
            Console.ResetColor();
            return true;
        }
        catch (JdeConnectionException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
            return false;
        }
    }

    static async Task TestWpfWorkflow(JdeClient.Core.JdeClient client)
    {
        Console.WriteLine("TEST 2: WPF Workflow Smoke Test");
        Console.WriteLine("===============================");

        Console.Write("Loading table catalog (F9860)... ");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tables = await LoadTableCatalogAsync(client, forceReload: true, currentObjectLocation);
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

        await TestTableSpecs(client, selectedTable.ObjectName, currentObjectLocation);
        await TestTableQueryStream(client, selectedTable.ObjectName, currentObjectLocation);
    }

    static async Task TestBusinessViewWorkflow(JdeClient.Core.JdeClient client)
    {
        Console.WriteLine("TEST 3: Business View Workflow Smoke Test");
        Console.WriteLine("=========================================");

        Console.Write("Loading business view catalog (F9860)... ");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var views = await LoadViewCatalogAsync(client, forceReload: true, currentObjectLocation);
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

        await TestBusinessViewSpecs(client, selectedView.ObjectName, currentObjectLocation);
        await TestBusinessViewQueryStream(client, selectedView.ObjectName, currentObjectLocation);
    }

    static async Task TestTableSpecs(
        JdeClient.Core.JdeClient client,
        string tableName,
        ObjectLocationSelection location)
    {
        Console.Write($"Loading specs for {tableName}... ");
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            var tableInfo = await client.GetTableInfoAsync(
                tableName,
                location.ObjectLibrarianDataSourceOverride,
                location.AllowFallback);
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

    static async Task TestBusinessViewSpecs(
        JdeClient.Core.JdeClient client,
        string viewName,
        ObjectLocationSelection location)
    {
        Console.Write($"Loading specs for {viewName}... ");
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            var viewInfo = await client.GetBusinessViewInfoAsync(
                viewName,
                location.ObjectLibrarianDataSourceOverride,
                location.AllowFallback);
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

    static Task TestTableQueryStream(
        JdeClient.Core.JdeClient client,
        string tableName,
        ObjectLocationSelection location)
    {
        const int maxRows = 25;
        Console.Write($"Streaming {tableName} (max {maxRows} rows)... ");
        try
        {
            var stream = client.QueryTableStream(
                tableName,
                filters: null,
                sorts: null,
                maxRows: maxRows,
                dataSourceOverride: location.QueryDataSourceOverride,
                indexId: null,
                allowDataSourceFallback: location.AllowFallback,
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

    static Task TestBusinessViewQueryStream(
        JdeClient.Core.JdeClient client,
        string viewName,
        ObjectLocationSelection location)
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
                dataSourceOverride: location.QueryDataSourceOverride,
                allowDataSourceFallback: location.AllowFallback,
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
            Console.WriteLine($"Current object location: {currentObjectLocation.DisplayName}");
            Console.WriteLine("0. Set object search pathcode/location");
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
            Console.WriteLine("16. Export project to PAR (OMW)");
            Console.WriteLine("17. Load C business function code");
            Console.WriteLine("18. Disconnect and exit");
            Console.WriteLine();
            Console.Write("Select option (0-18): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "0":
                    await SelectObjectLocationAsync(client);
                    break;
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
                    await ExportProjectToPar(client);
                    break;
                case "17":
                    await LoadBusinessFunctionCode(client);
                    break;
                case "18":
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

    static async Task SelectObjectLocationAsync(JdeClient.Core.JdeClient client)
    {
        var availablePathCodes = await LoadAvailablePathCodesAsync(client);
        var options = new List<ObjectLocationSelection> { ObjectLocationSelection.Local };
        options.AddRange(availablePathCodes.Select(ObjectLocationSelection.FromPathCode));

        Console.WriteLine($"\nChoose object search location (current: {currentObjectLocation.DisplayName}):");
        for (int i = 0; i < options.Count; i++)
        {
            Console.WriteLine($"  {i + 1,2}. {options[i].DisplayName}");
        }

        int customOption = options.Count + 1;
        Console.WriteLine($"  {customOption,2}. Custom path code");
        Console.Write($"Selection (1-{customOption}, Enter to keep current): ");
        string? selection = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(selection))
        {
            return;
        }

        if (int.TryParse(selection, out int selectedIndex))
        {
            if (selectedIndex >= 1 && selectedIndex <= options.Count)
            {
                currentObjectLocation = options[selectedIndex - 1];
                Console.WriteLine($"Object location set to {currentObjectLocation.DisplayName}.");
                return;
            }

            if (selectedIndex == customOption)
            {
                Console.Write("Enter custom path code (blank for Local): ");
                string? custom = Console.ReadLine();
                currentObjectLocation = ObjectLocationSelection.FromPathCode(custom);
                Console.WriteLine($"Object location set to {currentObjectLocation.DisplayName}.");
                return;
            }
        }

        // Allow entering the path code directly.
        currentObjectLocation = ObjectLocationSelection.FromPathCode(selection);
        Console.WriteLine($"Object location set to {currentObjectLocation.DisplayName}.");
    }

    static async Task<List<string>> LoadAvailablePathCodesAsync(JdeClient.Core.JdeClient client)
    {
        try
        {
            var pathCodes = await client.GetAvailablePathCodesAsync();
            return pathCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: unable to load path codes from F00942: {ex.Message}");
            return new List<string>();
        }
    }

    static async Task SearchTables(JdeClient.Core.JdeClient client, bool forceReload)
    {
        Console.Write("\nEnter table name pattern (wildcards with * supported): ");
        var pattern = Console.ReadLine()?.Trim() ?? string.Empty;
        var location = currentObjectLocation;
        string? objectLibrarianDataSourceOverride = location.ObjectLibrarianDataSourceOverride;
        bool allowObjectLibrarianFallback = location.AllowFallback;

        Console.WriteLine($"\nSearching for tables matching '{pattern}' in {location.DisplayName}...");
        List<JdeObjectInfo> matches;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            matches = await LoadTableCatalogAsync(client, forceReload, location);
        }
        else
        {
            matches = await client.GetObjectsAsync(
                JdeObjectType.Table,
                searchPattern: pattern,
                maxResults: 50000,
                dataSourceOverride: objectLibrarianDataSourceOverride,
                allowDataSourceFallback: allowObjectLibrarianFallback);
        }

        Console.WriteLine($"Found {matches.Count} matches in {location.DisplayName}:");
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
        lastTableSearchLocation = location;
        if (matches.Count == 1)
        {
            selectedTable = matches[0];
            selectedTableLocation = location;
            Console.WriteLine($"Selected {selectedTable.ObjectName}");
        }
    }

    static async Task SearchViews(JdeClient.Core.JdeClient client, bool forceReload)
    {
        Console.Write("\nEnter view name pattern (wildcards with * supported): ");
        var pattern = Console.ReadLine()?.Trim() ?? string.Empty;
        var location = currentObjectLocation;
        string? objectLibrarianDataSourceOverride = location.ObjectLibrarianDataSourceOverride;
        bool allowObjectLibrarianFallback = location.AllowFallback;

        Console.WriteLine($"\nSearching for views matching '{pattern}' in {location.DisplayName}...");
        List<JdeObjectInfo> matches;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            matches = await LoadViewCatalogAsync(client, forceReload, location);
        }
        else
        {
            matches = await client.GetObjectsAsync(
                JdeObjectType.BusinessView,
                searchPattern: pattern,
                maxResults: 50000,
                dataSourceOverride: objectLibrarianDataSourceOverride,
                allowDataSourceFallback: allowObjectLibrarianFallback);
        }

        Console.WriteLine($"Found {matches.Count} matches in {location.DisplayName}:");
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
        lastViewSearchLocation = location;
        if (matches.Count == 1)
        {
            selectedView = matches[0];
            selectedViewLocation = location;
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
        selectedTableLocation = lastTableSearchLocation;
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
        selectedViewLocation = lastViewSearchLocation;
        Console.WriteLine($"Selected {selectedView.ObjectName}");
    }

    static async Task OpenTableSpecs(JdeClient.Core.JdeClient client)
    {
        var (tableName, location) = ResolveSelectedTableContext();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        Console.WriteLine($"\nRetrieving specs for {tableName} from {location.DisplayName}...");
        var tableInfo = await client.GetTableInfoAsync(
            tableName,
            location.ObjectLibrarianDataSourceOverride,
            location.AllowFallback);

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
        var (viewName, location) = ResolveSelectedViewContext();
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return;
        }

        Console.WriteLine($"\nRetrieving specs for {viewName} from {location.DisplayName}...");
        var viewInfo = await client.GetBusinessViewInfoAsync(
            viewName,
            location.ObjectLibrarianDataSourceOverride,
            location.AllowFallback);

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

    static async Task ExportProjectToPar(JdeClient.Core.JdeClient client)
    {
        Console.Write("\nEnter OMW project name (OMWPRJID): ");
        string? projectName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            Console.WriteLine("Project name is required.");
            return;
        }

        Console.Write("Path code (blank to auto-detect): ");
        string? pathCode = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(pathCode))
        {
            pathCode = await ResolveProjectPathCodeAsync(client, projectName);
            if (string.IsNullOrWhiteSpace(pathCode))
            {
                Console.WriteLine("Path code is required to export the project.");
                return;
            }

            Console.WriteLine($"Using path code: {pathCode}");
        }

        Console.Write("Output folder or .par file (blank for JDE default): ");
        string? outputPath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = null;
        }

        Console.WriteLine("\nExporting project...");
        try
        {
            var result = await client.ExportProjectToParAsync(
                projectName,
                pathCode,
                outputPath: outputPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SUCCESS");
            Console.ResetColor();
            Console.WriteLine($"  Project: {result.ProjectName}");
            Console.WriteLine($"  Object: {result.ObjectId}");
            Console.WriteLine($"  Output: {result.OutputPath ?? "<JDE default>"}");
            Console.WriteLine($"  File already exists: {result.FileAlreadyExists}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
            Console.WriteLine($"  Error: {ex.Message}");
            if (ex is JdeApiException apiException)
            {
                if (!string.IsNullOrWhiteSpace(apiException.ApiFunction))
                {
                    Console.WriteLine($"  API: {apiException.ApiFunction}");
                }

                if (apiException.ResultCode.HasValue)
                {
                    Console.WriteLine($"  Result: {apiException.ResultCode.Value}");
                }
            }

            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
        }
    }

    static async Task<string?> ResolveProjectPathCodeAsync(JdeClient.Core.JdeClient client, string projectName)
    {
        Console.WriteLine("\nResolving path code from project objects...");
        List<JdeProjectObjectInfo> objects;
        try
        {
            objects = await client.GetProjectObjectsAsync(projectName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load project objects: {ex.Message}");
            return null;
        }

        var pathCodes = objects
            .Select(o => o.PathCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code)
            .ToList();

        if (pathCodes.Count == 0)
        {
            Console.WriteLine("No path codes found for this project.");
            return null;
        }

        if (pathCodes.Count == 1)
        {
            return pathCodes[0];
        }

        Console.WriteLine($"Found {pathCodes.Count} path codes:");
        for (int i = 0; i < pathCodes.Count; i++)
        {
            Console.WriteLine($"  {i + 1,2}. {pathCodes[i]}");
        }

        Console.Write("Select path code number or enter a value: ");
        string? selection = Console.ReadLine()?.Trim();
        if (int.TryParse(selection, out int index) && index >= 1 && index <= pathCodes.Count)
        {
            return pathCodes[index - 1];
        }

        return string.IsNullOrWhiteSpace(selection) ? null : selection;
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
        var (tableName, location) = ResolveSelectedTableContext();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        Console.WriteLine($"\nLoading columns for {tableName} from {location.DisplayName}...");
        var tableInfo = await client.GetTableInfoAsync(
            tableName,
            location.ObjectLibrarianDataSourceOverride,
            location.AllowFallback);
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
        var (viewName, location) = ResolveSelectedViewContext();
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return;
        }

        Console.WriteLine($"\nLoading columns for {viewName} from {location.DisplayName}...");
        var viewInfo = await client.GetBusinessViewInfoAsync(
            viewName,
            location.ObjectLibrarianDataSourceOverride,
            location.AllowFallback);
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
        var (tableName, location) = ResolveSelectedTableContext();
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
            dataSourceOverride: location.QueryDataSourceOverride,
            indexId: null,
            allowDataSourceFallback: location.AllowFallback,
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
        var (viewName, location) = ResolveSelectedViewContext();
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
            dataSourceOverride: location.QueryDataSourceOverride,
            allowDataSourceFallback: location.AllowFallback,
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

    static async Task<List<JdeObjectInfo>> LoadTableCatalogAsync(
        JdeClient.Core.JdeClient client,
        bool forceReload,
        ObjectLocationSelection location)
    {
        string cacheKey = location.CacheKey;
        if (!forceReload &&
            cachedTables != null &&
            cachedTables.Count > 0 &&
            string.Equals(cachedTablesLocationKey, cacheKey, StringComparison.OrdinalIgnoreCase))
        {
            return cachedTables;
        }

        var tables = await client.GetObjectsAsync(
            JdeObjectType.Table,
            maxResults: 50000,
            dataSourceOverride: location.ObjectLibrarianDataSourceOverride,
            allowDataSourceFallback: location.AllowFallback);
        cachedTables = tables;
        cachedTablesLocationKey = cacheKey;
        return tables;
    }

    static async Task<List<JdeObjectInfo>> LoadViewCatalogAsync(
        JdeClient.Core.JdeClient client,
        bool forceReload,
        ObjectLocationSelection location)
    {
        string cacheKey = location.CacheKey;
        if (!forceReload &&
            cachedViews != null &&
            cachedViews.Count > 0 &&
            string.Equals(cachedViewsLocationKey, cacheKey, StringComparison.OrdinalIgnoreCase))
        {
            return cachedViews;
        }

        var views = await client.GetObjectsAsync(
            JdeObjectType.BusinessView,
            maxResults: 50000,
            dataSourceOverride: location.ObjectLibrarianDataSourceOverride,
            allowDataSourceFallback: location.AllowFallback);
        cachedViews = views;
        cachedViewsLocationKey = cacheKey;
        return views;
    }

    static (string Name, ObjectLocationSelection Location) ResolveSelectedTableContext()
    {
        if (selectedTable != null)
        {
            return (selectedTable.ObjectName, selectedTableLocation);
        }

        Console.Write("\nEnter table name: ");
        return (Console.ReadLine()?.Trim() ?? string.Empty, currentObjectLocation);
    }

    static (string Name, ObjectLocationSelection Location) ResolveSelectedViewContext()
    {
        if (selectedView != null)
        {
            return (selectedView.ObjectName, selectedViewLocation);
        }

        Console.Write("\nEnter business view name: ");
        return (Console.ReadLine()?.Trim() ?? string.Empty, currentObjectLocation);
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

            await PromptSaveEventRulesXmlAsync(client, entry.Path, entry.Node.EventSpecKey, currentObjectLocation);
            if (!string.IsNullOrWhiteSpace(entry.Node.DataStructureName))
            {
                await PromptSaveDataStructureXmlAsync(client, entry.Path, entry.Node.DataStructureName, currentObjectLocation);
            }

        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.ResetColor();
        }
    }

    static async Task LoadBusinessFunctionCode(JdeClient.Core.JdeClient client)
    {
        Console.Write("\nEnter business function object (e.g. B5500725): ");
        string objectName = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            Console.WriteLine("Object name is required.");
            return;
        }

        Console.Write("Enter function name filter (optional): ");
        string? functionName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(functionName))
        {
            functionName = null;
        }

        var (location, dataSourceOverride) = PromptBusinessFunctionSourceLocation();
        string sourceLabel = location switch
        {
            JdeBusinessFunctionCodeLocation.Local => "Local",
            JdeBusinessFunctionCodeLocation.Central => string.IsNullOrWhiteSpace(dataSourceOverride)
                ? "Central (default)"
                : $"Central ({dataSourceOverride})",
            _ => "Auto (Local -> Central)"
        };

        Console.WriteLine($"\nLoading C business function payloads for {objectName} using {sourceLabel}...");
        try
        {
            var documents = await client.GetBusinessFunctionCodeAsync(
                objectName,
                functionName,
                location,
                dataSourceOverride);
            if (documents.Count == 0)
            {
                Console.WriteLine("No business function code records found.");
                return;
            }

            Console.WriteLine($"Loaded {documents.Count} record(s).");
            for (int i = 0; i < documents.Take(20).Count(); i++)
            {
                var doc = documents[i];
                string sourceHint = doc.SourceLooksLikeCode ? "code" : "text";
                string name = string.IsNullOrWhiteSpace(doc.FunctionName) ? "<unknown>" : doc.FunctionName;
                Console.WriteLine(
                    $"  {i + 1,3}. {name,-34} src={doc.SourceFileName,-15} bytes={doc.PayloadSize,7} {sourceHint}");
            }
            if (documents.Count > 20)
            {
                Console.WriteLine($"  ... and {documents.Count - 20} more");
            }

            var preview = documents[0];
            if (!string.IsNullOrWhiteSpace(preview.SourceCode))
            {
                Console.WriteLine("\nPreview (first 80 lines):");
                foreach (var line in preview.SourceCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Take(80))
                {
                    Console.WriteLine(line);
                }
            }
            else
            {
                Console.WriteLine("\nNo decoded source text was produced for the first record.");
            }

            await PromptSaveBusinessFunctionCodeAsync(objectName, documents);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.ResetColor();
        }
    }

    static (JdeBusinessFunctionCodeLocation Location, string? DataSourceOverride) PromptBusinessFunctionSourceLocation()
    {
        Console.WriteLine("\nChoose BUSFUNC source location:");
        Console.WriteLine("  1. Local");
        Console.WriteLine("  2. Central (PY920)");
        Console.WriteLine("  3. Central (PD920)");
        Console.WriteLine("  4. Auto (Local, then default central)");
        Console.WriteLine("  5. Central (custom path code/data source)");
        Console.Write("Selection (1-5, default 4): ");

        string selection = Console.ReadLine()?.Trim() ?? string.Empty;
        if (selection == "1")
        {
            return (JdeBusinessFunctionCodeLocation.Local, null);
        }

        if (selection == "2")
        {
            return (JdeBusinessFunctionCodeLocation.Central, "PY920");
        }

        if (selection == "3")
        {
            return (JdeBusinessFunctionCodeLocation.Central, "PD920");
        }

        if (selection == "5")
        {
            Console.Write("Central path code/data source override (e.g. PY920 or Central Objects - PY920): ");
            string? overrideInput = Console.ReadLine()?.Trim();
            return (JdeBusinessFunctionCodeLocation.Central, string.IsNullOrWhiteSpace(overrideInput) ? null : overrideInput);
        }

        return (JdeBusinessFunctionCodeLocation.Auto, null);
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

    static async Task PromptSaveEventRulesXmlAsync(
        JdeClient.Core.JdeClient client,
        string nodePath,
        string eventSpecKey,
        ObjectLocationSelection location)
    {
        Console.Write("\nSave XML to file(s)? (y/N): ");
        string? response = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(response) || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var documents = location.IsLocal
            ? await client.GetEventRulesXmlAsync(eventSpecKey)
            : await client.GetEventRulesXmlAsync(
                eventSpecKey,
                useCentralLocation: true,
                dataSourceOverride: location.CentralObjectsDataSourceOverride);
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

    static async Task PromptSaveDataStructureXmlAsync(
        JdeClient.Core.JdeClient client,
        string nodePath,
        string templateName,
        ObjectLocationSelection location)
    {
        Console.Write($"\nSave DSTMPL XML for {templateName}? (y/N): ");
        string? response = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(response) || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var documents = location.IsLocal
            ? await client.GetDataStructureXmlAsync(templateName)
            : await client.GetDataStructureXmlAsync(
                templateName,
                useCentralLocation: true,
                dataSourceOverride: location.CentralObjectsDataSourceOverride);
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

    static async Task PromptSaveBusinessFunctionCodeAsync(
        string objectName,
        IReadOnlyList<JdeBusinessFunctionCodeDocument> documents)
    {
        Console.Write("\nSave business function source/payload files? (y/N): ");
        string? response = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(response) || !response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Console.Write("Output folder (blank for current): ");
        string? folder = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Environment.CurrentDirectory;
        }

        Directory.CreateDirectory(folder);

        int fileCount = 0;
        foreach (var document in documents)
        {
            string functionName = string.IsNullOrWhiteSpace(document.FunctionName) ? "unknown_function" : document.FunctionName;
            string baseName = BuildBusinessFunctionFileName(objectName, functionName);

            if (!string.IsNullOrWhiteSpace(document.SourceCode))
            {
                string sourceExtension = document.SourceLooksLikeCode ? ".c" : ".txt";
                string sourcePath = Path.Combine(folder, $"{baseName}{sourceExtension}");
                await File.WriteAllTextAsync(sourcePath, document.SourceCode, Encoding.UTF8);
                fileCount++;
            }

            if (!string.IsNullOrWhiteSpace(document.HeaderCode))
            {
                string headerPath = Path.Combine(folder, $"{baseName}.h");
                await File.WriteAllTextAsync(headerPath, document.HeaderCode, Encoding.UTF8);
                fileCount++;
            }

            if (document.Payload.Length > 0)
            {
                string payloadPath = Path.Combine(folder, $"{baseName}.bin");
                await File.WriteAllBytesAsync(payloadPath, document.Payload);
                fileCount++;
            }
        }

        Console.WriteLine($"Saved {fileCount} file(s) to {folder}");
    }

    static string BuildBusinessFunctionFileName(string objectName, string functionName)
    {
        string baseName = $"BUSFUNC_{objectName}_{functionName}";
        return SanitizeFileName(TrimFileName(baseName, 120));
    }

    private sealed class ObjectLocationSelection
    {
        public static ObjectLocationSelection Local { get; } = new("Local", null);

        private ObjectLocationSelection(string label, string? pathCode)
        {
            Label = label;
            PathCode = string.IsNullOrWhiteSpace(pathCode) ? null : pathCode.Trim();
        }

        public string Label { get; }
        public string? PathCode { get; }

        public bool IsLocal => string.IsNullOrWhiteSpace(PathCode);
        public string DisplayName => IsLocal ? "Local" : PathCode!;
        public string CacheKey => IsLocal ? "LOCAL" : PathCode!;
        public bool AllowFallback => IsLocal;
        public string? ObjectLibrarianDataSourceOverride => IsLocal ? null : $"Object Librarian - {PathCode}";
        public string? CentralObjectsDataSourceOverride => IsLocal ? null : $"Central Objects - {PathCode}";
        public string? QueryDataSourceOverride => ObjectLibrarianDataSourceOverride;

        public static ObjectLocationSelection FromPathCode(string? pathCode)
        {
            if (string.IsNullOrWhiteSpace(pathCode))
            {
                return Local;
            }

            string normalized = pathCode.Trim();
            if (string.Equals(normalized, "local", StringComparison.OrdinalIgnoreCase))
            {
                return Local;
            }

            return new ObjectLocationSelection(normalized, normalized);
        }
    }

}

