using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Models;
using SpecLens.Avalonia.Models;
using SpecLens.Avalonia.Services;
using Serilog;
using ViewportGrid.Core.Interfaces;
using ViewportGrid.Core.Models;
using ViewportGrid.Data.Providers;

namespace SpecLens.Avalonia.ViewModels;

public sealed class SpecsTabViewModel : WorkspaceTabViewModel
{
    private static readonly IReadOnlyList<ViewportColumnDefinition> ColumnLayout = new[]
    {
        new ViewportColumnDefinition("Sequence", "#", 60),
        new ViewportColumnDefinition("Description", "Description", 220),
        new ViewportColumnDefinition("Alias", "Alias", 90),
        new ViewportColumnDefinition("Name", "Name", 160),
        new ViewportColumnDefinition("Type", "Type", 120),
        new ViewportColumnDefinition("SqlColumnName", "SQL Column", 120)
    };

    private static readonly IReadOnlyList<ViewportColumnDefinition> ViewColumnLayout = new[]
    {
        new ViewportColumnDefinition("Sequence", "#", 60),
        new ViewportColumnDefinition("Description", "Description", 220),
        new ViewportColumnDefinition("Alias", "Alias", 90),
        new ViewportColumnDefinition("Name", "Name", 160),
        new ViewportColumnDefinition("Type", "Type", 120),
        new ViewportColumnDefinition("SourceTable", "Table", 120),
        new ViewportColumnDefinition("SqlColumnName", "SQL Column", 120)
    };

    private static readonly IReadOnlyList<ViewportColumnDefinition> IndexLayout = new[]
    {
        new ViewportColumnDefinition("Name", "Index", 160),
        new ViewportColumnDefinition("PrimaryDisplay", "Primary", 70),
        new ViewportColumnDefinition("KeyColumnsDisplay", "Keys", 260)
    };

    private static readonly IReadOnlyList<ViewportColumnDefinition> ViewTableLayout = new[]
    {
        new ViewportColumnDefinition("TableName", "Table", 200),
        new ViewportColumnDefinition("InstanceCount", "Instances", 90),
        new ViewportColumnDefinition("PrimaryIndexId", "Primary Index", 120)
    };

    private static readonly IReadOnlyList<ViewportColumnDefinition> ViewJoinLayout = new[]
    {
        new ViewportColumnDefinition("JoinType", "Type", 130),
        new ViewportColumnDefinition("JoinExpression", "Join", 360)
    };

    private readonly IJdeConnectionService _connectionService;
    private readonly bool _isBusinessView;
    private readonly ObservableCollection<SpecColumnDisplay> _columns = new();
    private readonly ObservableCollection<SpecIndexDisplay> _indexes = new();
    private readonly ObservableCollection<SpecViewTableDisplay> _viewTables = new();
    private readonly ObservableCollection<SpecViewJoinDisplay> _viewJoins = new();
    private readonly InMemoryGridDataProvider _columnsViewportProvider = new();
    private readonly InMemoryGridDataProvider _indexesViewportProvider = new();
    private readonly InMemoryGridDataProvider _viewTablesViewportProvider = new();
    private readonly InMemoryGridDataProvider _viewJoinsViewportProvider = new();
    private readonly Dictionary<string, double> _tableColumnWidths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _viewColumnWidths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _indexColumnWidths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _viewTableColumnWidths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _viewJoinColumnWidths = new(StringComparer.OrdinalIgnoreCase);
    private string _tableDescription = string.Empty;
    private string _systemCode = string.Empty;
    private string _prefix = string.Empty;
    private int _columnCount;
    private int _columnsRowCount;
    private int _indexesRowCount;
    private int _viewTablesRowCount;
    private int _viewJoinsRowCount;
    private bool _isLoading;
    private string _statusMessage = "Loading specs...";
    private IReadOnlyList<ColumnMetadata> _columnsViewportColumns = Array.Empty<ColumnMetadata>();
    private IReadOnlyList<ColumnMetadata> _indexesViewportColumns = Array.Empty<ColumnMetadata>();
    private IReadOnlyList<ColumnMetadata> _viewTablesViewportColumns = Array.Empty<ColumnMetadata>();
    private IReadOnlyList<ColumnMetadata> _viewJoinsViewportColumns = Array.Empty<ColumnMetadata>();

    public SpecsTabViewModel(JdeObjectInfo jdeObject, IJdeConnectionService connectionService)
        : base($"Specs: {jdeObject.ObjectName}", $"specs_{jdeObject.ObjectName}_{Guid.NewGuid():N}")
    {
        TableName = jdeObject.ObjectName;
        _connectionService = connectionService;
        _isBusinessView = string.Equals(jdeObject.ObjectType?.Trim(), "BSVW", StringComparison.OrdinalIgnoreCase);
        Columns = new ReadOnlyObservableCollection<SpecColumnDisplay>(_columns);
        Indexes = new ReadOnlyObservableCollection<SpecIndexDisplay>(_indexes);
        ViewTables = new ReadOnlyObservableCollection<SpecViewTableDisplay>(_viewTables);
        ViewJoins = new ReadOnlyObservableCollection<SpecViewJoinDisplay>(_viewJoins);
        TableDescription = jdeObject.Description ?? string.Empty;
        SystemCode = jdeObject.SystemCode ?? string.Empty;
        Prefix = string.Empty;
    }

    public string TableName { get; }
    public ReadOnlyObservableCollection<SpecColumnDisplay> Columns { get; }
    public ReadOnlyObservableCollection<SpecIndexDisplay> Indexes { get; }
    public ReadOnlyObservableCollection<SpecViewTableDisplay> ViewTables { get; }
    public ReadOnlyObservableCollection<SpecViewJoinDisplay> ViewJoins { get; }
    public bool IsBusinessView => _isBusinessView;
    public bool IsTableSpec => !_isBusinessView;
    public IGridDataProvider ColumnsViewportDataProvider => _columnsViewportProvider;
    public IGridDataProvider IndexesViewportDataProvider => _indexesViewportProvider;
    public IGridDataProvider ViewTablesViewportDataProvider => _viewTablesViewportProvider;
    public IGridDataProvider ViewJoinsViewportDataProvider => _viewJoinsViewportProvider;

    public string TableDescription
    {
        get => _tableDescription;
        set => this.RaiseAndSetIfChanged(ref _tableDescription, value);
    }

    public string SystemCode
    {
        get => _systemCode;
        set => this.RaiseAndSetIfChanged(ref _systemCode, value);
    }

    public string Prefix
    {
        get => _prefix;
        set => this.RaiseAndSetIfChanged(ref _prefix, value);
    }

    public int ColumnCount
    {
        get => _columnCount;
        private set => this.RaiseAndSetIfChanged(ref _columnCount, value);
    }

    public int ColumnsRowCount
    {
        get => _columnsRowCount;
        private set => this.RaiseAndSetIfChanged(ref _columnsRowCount, value);
    }

    public int IndexesRowCount
    {
        get => _indexesRowCount;
        private set => this.RaiseAndSetIfChanged(ref _indexesRowCount, value);
    }

    public int ViewTablesRowCount
    {
        get => _viewTablesRowCount;
        private set => this.RaiseAndSetIfChanged(ref _viewTablesRowCount, value);
    }

    public int ViewJoinsRowCount
    {
        get => _viewJoinsRowCount;
        private set => this.RaiseAndSetIfChanged(ref _viewJoinsRowCount, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public IReadOnlyList<ColumnMetadata> ColumnsViewportColumns
    {
        get => _columnsViewportColumns;
        private set => this.RaiseAndSetIfChanged(ref _columnsViewportColumns, value);
    }

    public IReadOnlyList<ColumnMetadata> IndexesViewportColumns
    {
        get => _indexesViewportColumns;
        private set => this.RaiseAndSetIfChanged(ref _indexesViewportColumns, value);
    }

    public IReadOnlyList<ColumnMetadata> ViewTablesViewportColumns
    {
        get => _viewTablesViewportColumns;
        private set => this.RaiseAndSetIfChanged(ref _viewTablesViewportColumns, value);
    }

    public IReadOnlyList<ColumnMetadata> ViewJoinsViewportColumns
    {
        get => _viewJoinsViewportColumns;
        private set => this.RaiseAndSetIfChanged(ref _viewJoinsViewportColumns, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = $"Loading specs for {TableName}...";

        try
        {
            Log.Information("Specs tab loading {Table}", TableName);

            if (IsBusinessView)
            {
                await LoadBusinessViewSpecsAsync(cancellationToken);
            }
            else
            {
                await LoadTableSpecsAsync(cancellationToken);
            }
        }
        catch (JdeConnectionException ex)
        {
            StatusMessage = ex.Message;
            Log.Warning(ex, "Specs tab connection lost");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Log.Error(ex, "Specs tab failed for {Table}", TableName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadTableSpecsAsync(CancellationToken cancellationToken)
    {
        var metadata = await _connectionService.RunExclusiveAsync(async client =>
        {
            var tableInfo = await client.GetTableInfoAsync(TableName, cancellationToken);
            var indexes = await client.GetTableIndexesAsync(TableName, cancellationToken);
            return (tableInfo, indexes);
        }, cancellationToken);

        var tableInfo = metadata.tableInfo;
        var indexes = metadata.indexes;
        _columns.Clear();
        _indexes.Clear();
        _viewTables.Clear();
        _viewJoins.Clear();

        if (tableInfo != null)
        {
            var sqlPrefix = GetSqlPrefix(tableInfo.Columns);
            if (!string.IsNullOrWhiteSpace(sqlPrefix))
            {
                Prefix = sqlPrefix;
            }

            if (string.IsNullOrWhiteSpace(TableDescription) && !string.IsNullOrWhiteSpace(tableInfo.Description))
            {
                TableDescription = tableInfo.Description;
            }

            if (string.IsNullOrWhiteSpace(SystemCode) && !string.IsNullOrWhiteSpace(tableInfo.SystemCode))
            {
                SystemCode = tableInfo.SystemCode;
            }

            ColumnCount = tableInfo.Columns.Count;
            var aliases = tableInfo.Columns
                .Select(column => !string.IsNullOrWhiteSpace(column.DataDictionaryItem) ? column.DataDictionaryItem : column.Name)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dictionaryNames = await _connectionService.RunExclusiveAsync(
                client => client.GetDataDictionaryItemNamesAsync(aliases, cancellationToken),
                cancellationToken);
            var dictionaryDetails = await _connectionService.RunExclusiveAsync(
                client => client.GetDataDictionaryDetailsAsync(aliases, cancellationToken),
                cancellationToken);

            var nameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dictionaryNames)
            {
                if (string.IsNullOrWhiteSpace(item.DataItem))
                {
                    continue;
                }

                nameLookup.TryAdd(item.DataItem, item.Name ?? string.Empty);
            }

            var descriptionLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dictionaryDetails)
            {
                if (string.IsNullOrWhiteSpace(item.DataItem))
                {
                    continue;
                }

                string description = GetDictionaryText(item, 'A', 'R', 'C', 'H');
                if (!string.IsNullOrWhiteSpace(description))
                {
                    descriptionLookup.TryAdd(item.DataItem, description);
                }
            }

            for (int i = 0; i < tableInfo.Columns.Count; i++)
            {
                var column = tableInfo.Columns[i];
                string alias = column.DataDictionaryItem ?? column.Name ?? string.Empty;
                string name = alias;
                if (!string.IsNullOrWhiteSpace(alias) && nameLookup.TryGetValue(alias, out var lookupName) && !string.IsNullOrWhiteSpace(lookupName))
                {
                    name = lookupName;
                }

                string description = string.Empty;
                if (!string.IsNullOrWhiteSpace(alias) && descriptionLookup.TryGetValue(alias, out var lookupDescription))
                {
                    description = lookupDescription;
                }

                _columns.Add(new SpecColumnDisplay
                {
                    Sequence = i + 1,
                    Description = description,
                    Alias = alias,
                    Name = name,
                    Type = FormatColumnType(column),
                    SourceTable = TableName,
                    SqlColumnName = column.SqlName ?? string.Empty
                });
            }

            foreach (var index in indexes.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Name))
            {
                _indexes.Add(new SpecIndexDisplay
                {
                    Id = index.Id,
                    Name = index.Name,
                    IsPrimary = index.IsPrimary,
                    KeyColumns = index.KeyColumns ?? new List<string>()
                });
            }

            UpdateViewportData();
            StatusMessage = $"Loaded {tableInfo.Columns.Count} columns";
        }
        else
        {
            ClearViewportData();
            StatusMessage = "Table specs not found";
        }
    }

    private async Task LoadBusinessViewSpecsAsync(CancellationToken cancellationToken)
    {
        var viewInfo = await _connectionService.RunExclusiveAsync(
            client => client.GetBusinessViewInfoAsync(TableName, cancellationToken),
            cancellationToken);

        _columns.Clear();
        _indexes.Clear();
        _viewTables.Clear();
        _viewJoins.Clear();

        if (viewInfo != null)
        {
            Prefix = string.Empty;

            if (string.IsNullOrWhiteSpace(TableDescription) && !string.IsNullOrWhiteSpace(viewInfo.Description))
            {
                TableDescription = viewInfo.Description;
            }

            if (string.IsNullOrWhiteSpace(SystemCode) && !string.IsNullOrWhiteSpace(viewInfo.SystemCode))
            {
                SystemCode = viewInfo.SystemCode;
            }

            ColumnCount = viewInfo.Columns.Count;
            var aliases = viewInfo.Columns
                .Select(column => column.DataItem)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dictionaryNames = await _connectionService.RunExclusiveAsync(
                client => client.GetDataDictionaryItemNamesAsync(aliases, cancellationToken),
                cancellationToken);
            var dictionaryDetails = await _connectionService.RunExclusiveAsync(
                client => client.GetDataDictionaryDetailsAsync(aliases, cancellationToken),
                cancellationToken);

            var nameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dictionaryNames)
            {
                if (string.IsNullOrWhiteSpace(item.DataItem))
                {
                    continue;
                }

                nameLookup.TryAdd(item.DataItem, item.Name ?? string.Empty);
            }

            var descriptionLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dictionaryDetails)
            {
                if (string.IsNullOrWhiteSpace(item.DataItem))
                {
                    continue;
                }

                string description = GetDictionaryText(item, 'A', 'R', 'C', 'H');
                if (!string.IsNullOrWhiteSpace(description))
                {
                    descriptionLookup.TryAdd(item.DataItem, description);
                }
            }

            int displaySequence = 0;
            foreach (var column in viewInfo.Columns.OrderBy(column => column.Sequence))
            {
                string alias = column.DataItem;
                string name = alias;
                if (!string.IsNullOrWhiteSpace(alias) && nameLookup.TryGetValue(alias, out var lookupName) && !string.IsNullOrWhiteSpace(lookupName))
                {
                    name = lookupName;
                }

                string description = string.Empty;
                if (!string.IsNullOrWhiteSpace(alias) && descriptionLookup.TryGetValue(alias, out var lookupDescription))
                {
                    description = lookupDescription;
                }

                _columns.Add(new SpecColumnDisplay
                {
                    Sequence = ++displaySequence,
                    Description = description,
                    Alias = alias,
                    Name = name,
                    Type = FormatViewColumnType(column),
                    SourceTable = column.TableName,
                    SqlColumnName = string.Empty
                });
            }

            foreach (var table in viewInfo.Tables)
            {
                _viewTables.Add(new SpecViewTableDisplay
                {
                    TableName = table.TableName,
                    InstanceCount = table.InstanceCount,
                    PrimaryIndexId = table.PrimaryIndexId
                });
            }

            foreach (var join in viewInfo.Joins)
            {
                _viewJoins.Add(new SpecViewJoinDisplay
                {
                    JoinType = join.JoinType,
                    JoinExpression = BuildJoinExpression(join)
                });
            }

            UpdateViewportData();
            StatusMessage = $"Loaded {viewInfo.Columns.Count} columns";
        }
        else
        {
            ClearViewportData();
            StatusMessage = "View specs not found";
        }
    }

    private void UpdateViewportData()
    {
        if (IsBusinessView)
        {
            ColumnsViewportColumns = BuildViewportColumns(ViewColumnLayout, _viewColumnWidths);
            IndexesViewportColumns = Array.Empty<ColumnMetadata>();
            ViewTablesViewportColumns = BuildViewportColumns(ViewTableLayout, _viewTableColumnWidths);
            ViewJoinsViewportColumns = BuildViewportColumns(ViewJoinLayout, _viewJoinColumnWidths);

            _columnsViewportProvider.Reset(ViewColumnLayout.Select(column => column.Id).ToList());
            foreach (var column in _columns)
            {
                _columnsViewportProvider.AppendRow(new object?[]
                {
                    column.Sequence,
                    column.Description,
                    column.Alias,
                    column.Name,
                    column.Type,
                    column.SourceTable,
                    column.SqlColumnName
                });
            }
            ColumnsRowCount = _columns.Count;

            _viewTablesViewportProvider.Reset(ViewTableLayout.Select(column => column.Id).ToList());
            foreach (var table in _viewTables)
            {
                _viewTablesViewportProvider.AppendRow(new object?[]
                {
                    table.TableName,
                    table.InstanceCount,
                    table.PrimaryIndexId
                });
            }
            ViewTablesRowCount = _viewTables.Count;

            _viewJoinsViewportProvider.Reset(ViewJoinLayout.Select(column => column.Id).ToList());
            foreach (var join in _viewJoins)
            {
                _viewJoinsViewportProvider.AppendRow(new object?[]
                {
                    join.JoinType,
                    join.JoinExpression
                });
            }
            ViewJoinsRowCount = _viewJoins.Count;

            _indexesViewportProvider.Reset(Array.Empty<string>());
            IndexesRowCount = 0;
        }
        else
        {
            ColumnsViewportColumns = BuildViewportColumns(ColumnLayout, _tableColumnWidths);
            IndexesViewportColumns = BuildViewportColumns(IndexLayout, _indexColumnWidths);
            ViewTablesViewportColumns = Array.Empty<ColumnMetadata>();
            ViewJoinsViewportColumns = Array.Empty<ColumnMetadata>();

            _columnsViewportProvider.Reset(ColumnLayout.Select(column => column.Id).ToList());
            foreach (var column in _columns)
            {
                _columnsViewportProvider.AppendRow(new object?[]
                {
                    column.Sequence,
                    column.Description,
                    column.Alias,
                    column.Name,
                    column.Type,
                    column.SqlColumnName
                });
            }
            ColumnsRowCount = _columns.Count;

            _indexesViewportProvider.Reset(IndexLayout.Select(column => column.Id).ToList());
            foreach (var index in _indexes)
            {
                _indexesViewportProvider.AppendRow(new object?[]
                {
                    index.Name,
                    index.PrimaryDisplay,
                    index.KeyColumnsDisplay
                });
            }
            IndexesRowCount = _indexes.Count;

            _viewTablesViewportProvider.Reset(Array.Empty<string>());
            _viewJoinsViewportProvider.Reset(Array.Empty<string>());
            ViewTablesRowCount = 0;
            ViewJoinsRowCount = 0;
        }
    }

    private void ClearViewportData()
    {
        ColumnsViewportColumns = Array.Empty<ColumnMetadata>();
        IndexesViewportColumns = Array.Empty<ColumnMetadata>();
        ViewTablesViewportColumns = Array.Empty<ColumnMetadata>();
        ViewJoinsViewportColumns = Array.Empty<ColumnMetadata>();
        _columnsViewportProvider.Reset(Array.Empty<string>());
        _indexesViewportProvider.Reset(Array.Empty<string>());
        _viewTablesViewportProvider.Reset(Array.Empty<string>());
        _viewJoinsViewportProvider.Reset(Array.Empty<string>());
        ColumnsRowCount = 0;
        IndexesRowCount = 0;
        ViewTablesRowCount = 0;
        ViewJoinsRowCount = 0;
    }

    public void SetColumnsColumnWidth(string columnId, double width)
    {
        if (string.IsNullOrWhiteSpace(columnId))
        {
            return;
        }

        width = Math.Max(60, width);
        if (IsBusinessView)
        {
            _viewColumnWidths[columnId] = width;
            ColumnsViewportColumns = BuildViewportColumns(ViewColumnLayout, _viewColumnWidths);
        }
        else
        {
            _tableColumnWidths[columnId] = width;
            ColumnsViewportColumns = BuildViewportColumns(ColumnLayout, _tableColumnWidths);
        }
    }

    public void SetIndexesColumnWidth(string columnId, double width)
    {
        if (string.IsNullOrWhiteSpace(columnId))
        {
            return;
        }

        width = Math.Max(60, width);
        _indexColumnWidths[columnId] = width;
        IndexesViewportColumns = BuildViewportColumns(IndexLayout, _indexColumnWidths);
    }

    public void SetViewTablesColumnWidth(string columnId, double width)
    {
        if (string.IsNullOrWhiteSpace(columnId) || !IsBusinessView)
        {
            return;
        }

        width = Math.Max(60, width);
        _viewTableColumnWidths[columnId] = width;
        ViewTablesViewportColumns = BuildViewportColumns(ViewTableLayout, _viewTableColumnWidths);
    }

    public void SetViewJoinsColumnWidth(string columnId, double width)
    {
        if (string.IsNullOrWhiteSpace(columnId) || !IsBusinessView)
        {
            return;
        }

        width = Math.Max(60, width);
        _viewJoinColumnWidths[columnId] = width;
        ViewJoinsViewportColumns = BuildViewportColumns(ViewJoinLayout, _viewJoinColumnWidths);
    }

    private static IReadOnlyList<ColumnMetadata> BuildViewportColumns(
        IReadOnlyList<ViewportColumnDefinition> layout,
        IReadOnlyDictionary<string, double>? widthOverrides = null)
    {
        var columns = new List<ColumnMetadata>(layout.Count);
        for (int i = 0; i < layout.Count; i++)
        {
            var definition = layout[i];
            double width = definition.Width;
            if (widthOverrides != null && widthOverrides.TryGetValue(definition.Id, out var overrideWidth))
            {
                width = overrideWidth;
            }

            columns.Add(new ColumnMetadata
            {
                Id = definition.Id,
                DisplayName = definition.DisplayName,
                Width = width,
                DisplayIndex = i,
                IsFrozen = false
            });
        }

        return columns;
    }

    private static string GetTablePrefix(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return string.Empty;
        }

        int index = 0;
        while (index < tableName.Length && !char.IsDigit(tableName[index]))
        {
            index++;
        }

        return index > 0 ? tableName.Substring(0, index) : tableName;
    }

    private static string GetSqlPrefix(IReadOnlyList<JdeColumn> columns)
    {
        foreach (var column in columns)
        {
            if (!string.IsNullOrWhiteSpace(column.SqlName) && column.SqlName.Length >= 2)
            {
                return column.SqlName.Substring(0, 2);
            }
        }

        return string.Empty;
    }

    private static string FormatColumnType(JdeColumn column)
    {
        return column.DataType switch
        {
            JdeClient.Core.Interop.JdeStructures.EVDT_CHAR => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_STRING => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_VARSTRING => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_TEXT => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_LONGVARCHAR => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_JDEDATE => "Date",
            JdeClient.Core.Interop.JdeStructures.EVDT_MATH_NUMERIC => column.Decimals > 0
                ? $"Numeric({column.Length},{column.Decimals})"
                : $"Numeric({column.Length})",
            _ => column.Decimals > 0
                ? $"Number({column.Length},{column.Decimals})"
                : $"Number({column.Length})"
        };
    }

    private static string FormatViewColumnType(JdeBusinessViewColumn column)
    {
        return column.DataType switch
        {
            JdeClient.Core.Interop.JdeStructures.EVDT_CHAR => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_STRING => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_VARSTRING => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_TEXT => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_LONGVARCHAR => $"String({column.Length})",
            JdeClient.Core.Interop.JdeStructures.EVDT_JDEDATE => "Date",
            JdeClient.Core.Interop.JdeStructures.EVDT_MATH_NUMERIC => column.Decimals > 0
                ? $"Numeric({column.Length},{column.Decimals})"
                : $"Numeric({column.Length})",
            _ => column.Decimals > 0
                ? $"Number({column.Length},{column.Decimals})"
                : $"Number({column.Length})"
        };
    }

    private static string BuildJoinExpression(JdeBusinessViewJoin join)
    {
        string left = BuildQualifiedName(join.ForeignTable, join.ForeignColumn);
        string right = BuildQualifiedName(join.PrimaryTable, join.PrimaryColumn);
        string op = string.IsNullOrWhiteSpace(join.JoinOperator) ? "=" : join.JoinOperator;

        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} {op} {right}";
    }

    private static string BuildQualifiedName(string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return columnName ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            return tableName;
        }

        return $"{tableName}.{columnName}";
    }

    private static string GetDictionaryText(JdeDataDictionaryDetails details, params char[] textTypes)
    {
        if (details.Texts.Count == 0 || textTypes.Length == 0)
        {
            return string.Empty;
        }

        foreach (var textType in textTypes)
        {
            var match = details.Texts.FirstOrDefault(text => text.TextType == textType);
            if (match != null && !string.IsNullOrWhiteSpace(match.Text))
            {
                return match.Text.Trim();
            }
        }

        return string.Empty;
    }

    private sealed record ViewportColumnDefinition(string Id, string DisplayName, double Width);
}
