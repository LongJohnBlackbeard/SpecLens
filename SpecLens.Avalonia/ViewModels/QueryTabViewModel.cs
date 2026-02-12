using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Threading;
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

public sealed class QueryTabViewModel : WorkspaceTabViewModel
{
    private static readonly ConcurrentDictionary<string, JdeDataDictionaryTitle> DataDictionaryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IJdeConnectionService _connectionService;
    private readonly IAppSettingsService _settingsService;
    private readonly IDataDictionaryInfoService _dataDictionaryInfoService;
    private readonly bool _isBusinessView;
    private readonly string? _objectLibrarianDataSourceOverride;
    private readonly bool _allowObjectLibrarianFallback;
    private readonly ObservableCollection<ColumnFilter> _filters = new();
    private readonly ObservableCollection<JdeDataSourceInfo> _dataSources = new();
    private readonly ObservableCollection<JdeIndexInfo> _indexes = new();
    private readonly ObservableCollection<string> _columnNames = new();
    private readonly Dictionary<string, JdeColumn> _columnsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _columnWidthOverrides = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _currentColumns = Array.Empty<string>();
    private CancellationTokenSource? _queryCts;
    private ManualResetEventSlim? _pauseEvent;
    private int _rowCount;
    private readonly List<ColumnFilter> _sortedFilters = new();
    private readonly List<string> _columnOrder = new();
    private readonly InMemoryGridDataProvider _viewportProvider = new();
    private const double DefaultColumnWidth = 160.0;
    private const double HeaderRowHeightSingleLine = 26.0;
    private const double HeaderRowHeightWithDescription = 36.0;
    private const double HeaderFilterRowHeight = 30.0;
    private ObservableCollection<Dictionary<string, object?>> _queryResults = new();
    private bool _isLoading;
    private bool _isQueryRunning;
    private bool _isQueryPaused;
    private string _statusMessage = "Ready";
    private double _queryColumnWidth = DefaultColumnWidth;
    private JdeDataSourceInfo? _selectedDataSource;
    private JdeIndexInfo? _selectedIndex;
    private string? _selectedColumnName;
    private bool _useViewportGrid = true;
    private IReadOnlyList<ColumnMetadata> _viewportColumns = Array.Empty<ColumnMetadata>();
    private IReadOnlyList<ViewportHeaderColumn> _frozenHeaderColumns = Array.Empty<ViewportHeaderColumn>();
    private IReadOnlyList<ViewportHeaderColumn> _scrollableHeaderColumns = Array.Empty<ViewportHeaderColumn>();
    private double _frozenHeaderWidth;
    private double _scrollableHeaderWidth;
    private IGridDataProvider? _viewportDataProvider;
    private int _frozenColumnCount;
    private double _headerRowHeight = HeaderRowHeightSingleLine;
    private double _headerTotalHeight = HeaderRowHeightSingleLine + HeaderFilterRowHeight;

    public QueryTabViewModel(
        string tableName,
        IJdeConnectionService connectionService,
        IAppSettingsService settingsService,
        IDataDictionaryInfoService dataDictionaryInfoService,
        bool isBusinessView = false,
        string? objectLibrarianDataSourceOverride = null,
        string? locationLabel = null)
        : base(
            $"Query: {tableName}",
            $"query_{tableName}_{(string.IsNullOrWhiteSpace(locationLabel) ? "local" : locationLabel)}_{Guid.NewGuid():N}")
    {
        TableName = tableName;
        _connectionService = connectionService;
        _settingsService = settingsService;
        _dataDictionaryInfoService = dataDictionaryInfoService;
        _isBusinessView = isBusinessView;
        _objectLibrarianDataSourceOverride = objectLibrarianDataSourceOverride;
        _allowObjectLibrarianFallback = string.IsNullOrWhiteSpace(objectLibrarianDataSourceOverride);
        QueryColumnWidth = _settingsService.Current.QueryColumnWidth > 0 ? _settingsService.Current.QueryColumnWidth : DefaultColumnWidth;
        Filters = new ReadOnlyObservableCollection<ColumnFilter>(_filters);
        DataSources = new ReadOnlyObservableCollection<JdeDataSourceInfo>(_dataSources);
        Indexes = new ReadOnlyObservableCollection<JdeIndexInfo>(_indexes);
        ColumnNames = new ReadOnlyObservableCollection<string>(_columnNames);
        _settingsService.SettingsChanged += OnSettingsChanged;
        ViewportDataProvider = _viewportProvider;

        var filterCountChanged = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => _filters.CollectionChanged += h,
                h => _filters.CollectionChanged -= h)
            .Select(_ => _filters.Count)
            .StartWith(_filters.Count);

        var canRun = this.WhenAnyValue(x => x.IsQueryRunning, x => x.IsQueryPaused,
                (running, paused) => new { running, paused })
            .CombineLatest(filterCountChanged, (state, count) => count > 0 && (!state.running || state.paused));
        RunQueryCommand = ReactiveCommand.Create(StartOrResumeQuery, canRun);

        var canPause = this.WhenAnyValue(x => x.IsQueryRunning, x => x.IsQueryPaused,
            (running, paused) => running && !paused);
        PauseQueryCommand = ReactiveCommand.Create(TogglePause, canPause);

        var canStop = this.WhenAnyValue(x => x.IsQueryRunning);
        StopQueryCommand = ReactiveCommand.Create(StopQuery, canStop);

        var canClear = this.WhenAnyValue(x => x.IsQueryRunning, running => !running);
        ClearQueryCommand = ReactiveCommand.Create(ClearQuery, canClear);
    }

    public string TableName { get; }
    public ReadOnlyObservableCollection<ColumnFilter> Filters { get; }
    public ReadOnlyObservableCollection<JdeDataSourceInfo> DataSources { get; }
    public ReadOnlyObservableCollection<JdeIndexInfo> Indexes { get; }
    public ReadOnlyObservableCollection<string> ColumnNames { get; }
    public bool IsBusinessView => _isBusinessView;
    public IDataDictionaryInfoService DataDictionaryInfo => _dataDictionaryInfoService;

    public ObservableCollection<Dictionary<string, object?>> QueryResults
    {
        get => _queryResults;
        private set => this.RaiseAndSetIfChanged(ref _queryResults, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool IsQueryRunning
    {
        get => _isQueryRunning;
        private set => this.RaiseAndSetIfChanged(ref _isQueryRunning, value);
    }

    public bool IsQueryPaused
    {
        get => _isQueryPaused;
        private set => this.RaiseAndSetIfChanged(ref _isQueryPaused, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public double QueryColumnWidth
    {
        get => _queryColumnWidth;
        set => this.RaiseAndSetIfChanged(ref _queryColumnWidth, value);
    }

    public int RowCount
    {
        get => _rowCount;
        private set => this.RaiseAndSetIfChanged(ref _rowCount, value);
    }

    public JdeDataSourceInfo? SelectedDataSource
    {
        get => _selectedDataSource;
        set => this.RaiseAndSetIfChanged(ref _selectedDataSource, value);
    }

    public JdeIndexInfo? SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (EqualityComparer<JdeIndexInfo?>.Default.Equals(_selectedIndex, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedIndex, value);
            ApplyIndexKeyHighlights();
        }
    }

    public string? SelectedColumnName
    {
        get => _selectedColumnName;
        set => this.RaiseAndSetIfChanged(ref _selectedColumnName, value);
    }

    public bool UseViewportGrid
    {
        get => _useViewportGrid;
        set => this.RaiseAndSetIfChanged(ref _useViewportGrid, value);
    }

    public IReadOnlyList<ColumnMetadata> ViewportColumns
    {
        get => _viewportColumns;
        private set => this.RaiseAndSetIfChanged(ref _viewportColumns, value);
    }

    public IReadOnlyList<ViewportHeaderColumn> FrozenHeaderColumns
    {
        get => _frozenHeaderColumns;
        private set => this.RaiseAndSetIfChanged(ref _frozenHeaderColumns, value);
    }

    public IReadOnlyList<ViewportHeaderColumn> ScrollableHeaderColumns
    {
        get => _scrollableHeaderColumns;
        private set => this.RaiseAndSetIfChanged(ref _scrollableHeaderColumns, value);
    }

    public double FrozenHeaderWidth
    {
        get => _frozenHeaderWidth;
        private set => this.RaiseAndSetIfChanged(ref _frozenHeaderWidth, value);
    }

    public double ScrollableHeaderWidth
    {
        get => _scrollableHeaderWidth;
        private set => this.RaiseAndSetIfChanged(ref _scrollableHeaderWidth, value);
    }

    public IGridDataProvider? ViewportDataProvider
    {
        get => _viewportDataProvider;
        private set => this.RaiseAndSetIfChanged(ref _viewportDataProvider, value);
    }

    public int FrozenColumnCount
    {
        get => _frozenColumnCount;
        set
        {
            if (_frozenColumnCount == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _frozenColumnCount, value);
            UpdateViewportColumns(_currentColumns);
        }
    }

    public double HeaderRowHeight
    {
        get => _headerRowHeight;
        private set => this.RaiseAndSetIfChanged(ref _headerRowHeight, value);
    }

    public double HeaderTotalHeight
    {
        get => _headerTotalHeight;
        private set => this.RaiseAndSetIfChanged(ref _headerTotalHeight, value);
    }

    public ReactiveCommand<Unit, Unit> RunQueryCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseQueryCommand { get; }
    public ReactiveCommand<Unit, Unit> StopQueryCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearQueryCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = $"Loading columns for {TableName}...";

        try
        {
            var metadata = await _connectionService.RunExclusiveAsync(async client =>
            {
                var dataSources = await client.GetAvailableDataSourcesAsync(cancellationToken: cancellationToken);
                var defaultDataSource = await client.GetDefaultTableDataSourceAsync(TableName, cancellationToken);
                JdeTableInfo? tableInfo = null;
                JdeBusinessViewInfo? viewInfo = null;
                var indexes = new List<JdeIndexInfo>();

                if (_isBusinessView)
                {
                    viewInfo = await client.GetBusinessViewInfoAsync(
                        TableName,
                        _objectLibrarianDataSourceOverride,
                        _allowObjectLibrarianFallback,
                        cancellationToken);
                }
                else
                {
                    tableInfo = await client.GetTableInfoAsync(
                        TableName,
                        _objectLibrarianDataSourceOverride,
                        _allowObjectLibrarianFallback,
                        cancellationToken);
                    indexes = await client.GetTableIndexesAsync(
                        TableName,
                        _objectLibrarianDataSourceOverride,
                        _allowObjectLibrarianFallback,
                        cancellationToken);
                }

                return (tableInfo, viewInfo, dataSources, defaultDataSource, indexes);
            }, cancellationToken);

            var tableInfo = metadata.tableInfo;
            var viewInfo = metadata.viewInfo;
            var dataSources = metadata.dataSources;
            var defaultDataSource = metadata.defaultDataSource;
            var indexes = metadata.indexes;
            _filters.Clear();
            _dataSources.Clear();
            _indexes.Clear();
            _columnNames.Clear();
            _columnsByName.Clear();

            var resolvedColumns = _isBusinessView
                ? BuildViewColumns(viewInfo)
                : tableInfo?.Columns ?? new List<JdeColumn>();

            if (resolvedColumns.Count > 0)
            {
                var columnNameList = new List<string>();
                foreach (var column in resolvedColumns)
                {
                    _columnsByName[column.Name] = column;
                    if (!string.IsNullOrWhiteSpace(column.DataDictionaryItem))
                    {
                        _columnsByName[column.DataDictionaryItem] = column;
                    }
                    if (!string.IsNullOrWhiteSpace(column.SqlName))
                    {
                        _columnsByName[column.SqlName] = column;
                    }
                    _filters.Add(new ColumnFilter(column.Name, column.SqlName, column.Description, column.DataDictionaryItem));
                    if (!string.IsNullOrWhiteSpace(column.Name))
                    {
                        columnNameList.Add(column.Name);
                    }
                }
                foreach (var columnName in columnNameList.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    _columnNames.Add(columnName);
                }

                ResetSorting();
                UpdateHeaderDisplay();
                InitializeQueryResults(resolvedColumns.Select(c => c.Name).ToList());
                RowCount = 0;
                StatusMessage = "Ready";
            }
            else
            {
                StatusMessage = _isBusinessView ? "View not found" : "Table not found";
            }

            foreach (var dataSource in dataSources)
            {
                _dataSources.Add(dataSource);
            }

            SelectedDataSource = _dataSources
                .FirstOrDefault(ds => string.Equals(ds.Name, _objectLibrarianDataSourceOverride, StringComparison.OrdinalIgnoreCase))
                ?? _dataSources
                .FirstOrDefault(ds => string.Equals(ds.Name, defaultDataSource, StringComparison.OrdinalIgnoreCase))
                ?? _dataSources.FirstOrDefault();

            if (!_isBusinessView)
            {
                foreach (var index in indexes.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Name))
                {
                    _indexes.Add(index);
                }

                SelectedIndex = _indexes.FirstOrDefault(i => i.IsPrimary) ?? _indexes.FirstOrDefault();
                ApplyIndexKeyHighlights();
            }
            else
            {
                SelectedIndex = null;
            }

            if (_filters.Count > 0)
            {
                await LoadDataDictionaryDescriptionsAsync(cancellationToken);
                UpdateHeaderDisplay();
            }
        }
        catch (JdeConnectionException ex)
        {
            StatusMessage = ex.Message;
            Log.Warning(ex, "Query tab connection lost");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Log.Error(ex, "Query tab init failed for {Table}", TableName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartOrResumeQuery()
    {
        if (IsQueryPaused)
        {
            IsQueryPaused = false;
            _pauseEvent?.Set();
            StatusMessage = $"Querying {TableName}...";
            return;
        }

        if (IsQueryRunning)
        {
            return;
        }

        _ = RunQueryAsync();
    }

    private async Task RunQueryAsync()
    {
        if (IsQueryPaused)
        {
            IsQueryPaused = false;
            _pauseEvent?.Set();
            StatusMessage = $"Querying {TableName}...";
            return;
        }

        if (IsQueryRunning)
        {
            return;
        }

        IsQueryRunning = true;
        IsQueryPaused = false;
        StatusMessage = $"Querying {TableName}...";
        RowCount = 0;

        _queryCts?.Cancel();
        _queryCts?.Dispose();
        _pauseEvent?.Dispose();

        _queryCts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        var ct = _queryCts.Token;

        try
        {
            var activeFilters = _filters
                .Select(f => new { f.Name, f.Value })
                .Select(f => TryParseFilterInput(f.Value, out var op, out var value)
                    ? new JdeFilter { ColumnName = f.Name, Value = value, Operator = op }
                    : null)
                .Where(f => f != null)
                .Cast<JdeFilter>()
                .ToList();
            var activeSorts = BuildSorts();

            Log.Information("Query tab start {Table} with {FilterCount} filters", TableName, activeFilters.Count);

            await _connectionService.RunExclusiveAsync(
                client => Task.Run(async () =>
                {
                    bool shouldPopulateResults = !UseViewportGrid;
                    bool allowDataSourceFallback = SelectedDataSource == null || string.IsNullOrWhiteSpace(SelectedDataSource.Name);
                    var stream = _isBusinessView
                        ? client.QueryViewStream(
                            TableName,
                            activeFilters,
                            activeSorts,
                            maxRows: 0,
                            dataSourceOverride: SelectedDataSource?.Name,
                            allowDataSourceFallback: allowDataSourceFallback,
                            cancellationToken: ct)
                        : client.QueryTableStream(
                            TableName,
                            activeFilters,
                            activeSorts,
                            maxRows: 0,
                            dataSourceOverride: SelectedDataSource?.Name,
                            indexId: SelectedIndex?.Id,
                            allowDataSourceFallback: allowDataSourceFallback,
                            cancellationToken: ct);

                    Dispatcher.UIThread.Invoke(() =>
                    {
                        InitializeQueryResults(stream.ColumnNames);
                    }, DispatcherPriority.Send);

                    const int batchSize = 100;
                    var batch = shouldPopulateResults
                        ? new List<Dictionary<string, object?>>(batchSize)
                        : null;
                    int pendingUiUpdates = 0;
                    int totalRows = 0;

                    foreach (var row in stream)
                    {
                        ct.ThrowIfCancellationRequested();
                        _pauseEvent?.Wait(ct);

                        var rowValues = new object?[stream.ColumnNames.Count];
                        Dictionary<string, object?>? rowData = shouldPopulateResults
                            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                            : null;
                        int columnIndex = 0;

                        foreach (var column in stream.ColumnNames)
                        {
                            object? cellValue;
                            if (row.TryGetValue(column, out var value) && value != null && value != DBNull.Value)
                            {
                                cellValue = value;
                            }
                            else
                            {
                                cellValue = string.Empty;
                            }

                            if (rowData != null)
                            {
                                rowData[column] = cellValue;
                            }
                            rowValues[columnIndex++] = cellValue;
                        }

                        _viewportProvider.AppendRow(rowValues);

                        if (rowData != null)
                        {
                            batch!.Add(rowData);
                        }
                        else
                        {
                            totalRows++;
                            if (totalRows % batchSize == 0)
                            {
                                int snapshot = totalRows;
                                Dispatcher.UIThread.Post(() =>
                                {
                                    if (ct.IsCancellationRequested) return;
                                    RowCount = snapshot;
                                }, DispatcherPriority.Normal);
                            }
                        }

                        if (shouldPopulateResults && batch!.Count >= batchSize)
                        {
                            var currentBatch = batch.ToList();
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (ct.IsCancellationRequested) return;
                                foreach (var item in currentBatch)
                                {
                                    QueryResults.Add(item);
                                }
                                RowCount = RowCount + currentBatch.Count;
                                Interlocked.Decrement(ref pendingUiUpdates);
                            }, DispatcherPriority.Normal);

                            Interlocked.Increment(ref pendingUiUpdates);
                            batch.Clear();

                            while (pendingUiUpdates > 5)
                            {
                                ct.ThrowIfCancellationRequested();
                                await Task.Delay(10, ct).ConfigureAwait(false);
                            }
                        }
                    }

                    if (shouldPopulateResults && batch != null && batch.Count > 0)
                    {
                        var finalBatch = batch.ToList();
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            foreach (var item in finalBatch)
                            {
                                QueryResults.Add(item);
                            }
                            RowCount = RowCount + finalBatch.Count;
                        }, DispatcherPriority.Normal);
                    }
                    else if (!shouldPopulateResults && totalRows > 0)
                    {
                        int snapshot = totalRows;
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            RowCount = snapshot;
                        }, DispatcherPriority.Normal);
                    }

                    if (shouldPopulateResults)
                    {
                        while (pendingUiUpdates > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            await Task.Delay(10, ct).ConfigureAwait(false);
                        }
                    }
                }, ct),
                ct);

            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            StatusMessage = "Query complete";
            Log.Information("Query tab complete for {Table}", TableName);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Query canceled";
            Log.Warning("Query tab canceled for {Table}", TableName);
        }
        catch (JdeTableException ex)
        {
            StatusMessage = SelectedDataSource != null
                ? $"Table not available in data source {SelectedDataSource.Name}"
                : ex.Message;
            Log.Warning(ex, "Query tab failed for {Table}", TableName);
        }
        catch (JdeConnectionException ex)
        {
            StatusMessage = ex.Message;
            Log.Warning(ex, "Query tab connection lost");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Log.Error(ex, "Query tab failed for {Table}", TableName);
        }
        finally
        {
            IsQueryRunning = false;
            IsQueryPaused = false;
        }
    }

    private void TogglePause()
    {
        if (!IsQueryRunning || IsQueryPaused)
        {
            return;
        }

        IsQueryPaused = true;
        _pauseEvent?.Reset();
        StatusMessage = "Query paused";
    }

    private void InitializeQueryResults(IReadOnlyList<string> columnNames)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(() => InitializeQueryResults(columnNames));
            return;
        }

        if (HasMatchingSchema(columnNames))
        {
            QueryResults.Clear();
        }
        else
        {
            _currentColumns = columnNames.ToList();
            _columnWidthOverrides.Clear();
            QueryResults = new ObservableCollection<Dictionary<string, object?>>();
        }

        _viewportProvider.Reset(_currentColumns);
        UpdateViewportColumns(_currentColumns);
            RowCount = 0;
    }

    private bool HasMatchingSchema(IReadOnlyList<string> columnNames)
    {
        if (_currentColumns.Count != columnNames.Count)
        {
            return false;
        }

        for (int i = 0; i < columnNames.Count; i++)
        {
            var existing = _currentColumns[i];
            var expected = columnNames[i];
            if (!string.Equals(existing, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateViewportColumns(IReadOnlyList<string> columnNames)
    {
        var orderedNames = GetOrderedColumns(columnNames);
        _viewportProvider.SetDisplayOrder(orderedNames);
        if (orderedNames.Count == 0)
        {
            ViewportColumns = Array.Empty<ColumnMetadata>();
            FrozenHeaderColumns = Array.Empty<ViewportHeaderColumn>();
            ScrollableHeaderColumns = Array.Empty<ViewportHeaderColumn>();
            FrozenHeaderWidth = 0;
            ScrollableHeaderWidth = 0;
            return;
        }

        var headerLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var filterLookup = new Dictionary<string, ColumnFilter>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in _filters)
        {
            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                headerLookup[filter.Name] = filter.HeaderText;
                filterLookup[filter.Name] = filter;
            }
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, orderedNames.Count);
        var columns = new List<ColumnMetadata>(orderedNames.Count);
        var headerColumns = new List<ViewportHeaderColumn>(orderedNames.Count);
        for (int i = 0; i < orderedNames.Count; i++)
        {
            string name = orderedNames[i];
            string displayName = headerLookup.TryGetValue(name, out var header) ? header : name;
            double width = _columnWidthOverrides.TryGetValue(name, out var overrideWidth)
                ? overrideWidth
                : QueryColumnWidth;
            var column = new ColumnMetadata
            {
                Id = name,
                DisplayName = displayName,
                Width = width,
                DisplayIndex = i,
                IsFrozen = i < frozenCount
            };
            columns.Add(column);
            if (!filterLookup.TryGetValue(name, out var filter))
            {
                filter = new ColumnFilter(name);
            }

            headerColumns.Add(new ViewportHeaderColumn(column, filter));
        }

        ViewportColumns = columns;
        UpdateHeaderColumns(headerColumns, frozenCount);
    }

    private void UpdateHeaderColumns(IReadOnlyList<ViewportHeaderColumn> headers, int frozenCount)
    {
        if (headers.Count == 0)
        {
            FrozenHeaderColumns = Array.Empty<ViewportHeaderColumn>();
            ScrollableHeaderColumns = Array.Empty<ViewportHeaderColumn>();
            FrozenHeaderWidth = 0;
            ScrollableHeaderWidth = 0;
            return;
        }

        frozenCount = Math.Clamp(frozenCount, 0, headers.Count);
        var frozen = new List<ViewportHeaderColumn>(frozenCount);
        for (int i = 0; i < frozenCount; i++)
        {
            frozen.Add(headers[i]);
        }

        var scrollable = new List<ViewportHeaderColumn>(headers.Count - frozenCount);
        for (int i = frozenCount; i < headers.Count; i++)
        {
            scrollable.Add(headers[i]);
        }

        FrozenHeaderColumns = frozen;
        ScrollableHeaderColumns = scrollable;

        double width = 0;
        foreach (var header in frozen)
        {
            width += header.Width;
        }

        FrozenHeaderWidth = width;

        width = 0;
        foreach (var header in scrollable)
        {
            width += header.Width;
        }

        ScrollableHeaderWidth = width;
    }

    private static List<JdeColumn> BuildViewColumns(JdeBusinessViewInfo? viewInfo)
    {
        var columns = new List<JdeColumn>();
        if (viewInfo == null || viewInfo.Columns.Count == 0)
        {
            return columns;
        }

        foreach (var column in viewInfo.Columns.OrderBy(c => c.Sequence))
        {
            if (string.IsNullOrWhiteSpace(column.DataItem))
            {
                continue;
            }

            columns.Add(new JdeColumn
            {
                Name = BuildViewColumnName(column.TableName, column.DataItem, column.InstanceId),
                DataDictionaryItem = column.DataItem,
                SqlName = column.DataItem,
                DataType = column.DataType,
                Length = column.Length,
                Decimals = column.Decimals,
                SourceTable = column.TableName,
                InstanceId = column.InstanceId
            });
        }

        return columns;
    }

    private static string BuildViewColumnName(string? tableName, string dataItem, int instanceId)
    {
        if (instanceId > 0)
        {
            return string.IsNullOrWhiteSpace(tableName)
                ? $"{dataItem}({instanceId})"
                : $"{tableName}({instanceId}).{dataItem}";
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return dataItem;
        }

        return $"{tableName}.{dataItem}";
    }

    private static bool IsStringType(int evdType)
    {
        return evdType switch
        {
            JdeClient.Core.Interop.JdeStructures.EVDT_CHAR => true,
            JdeClient.Core.Interop.JdeStructures.EVDT_STRING => true,
            JdeClient.Core.Interop.JdeStructures.EVDT_VARSTRING => true,
            JdeClient.Core.Interop.JdeStructures.EVDT_TEXT => true,
            JdeClient.Core.Interop.JdeStructures.EVDT_LONGVARCHAR => true,
            _ => false
        };
    }

    public bool TryGetStringColumnLength(string columnName, out int length)
    {
        length = 0;
        if (_columnsByName.TryGetValue(columnName, out var column) && IsStringType(column.DataType))
        {
            length = column.Length;
            return true;
        }

        return false;
    }

    public bool TryGetDataDictionaryItem(string columnName, out string dataItem)
    {
        dataItem = string.Empty;
        if (!_columnsByName.TryGetValue(columnName, out var column))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(column.DataDictionaryItem))
        {
            dataItem = column.DataDictionaryItem;
        }
        else if (!string.IsNullOrWhiteSpace(column.Name))
        {
            dataItem = column.Name;
        }

        return !string.IsNullOrWhiteSpace(dataItem);
    }

    private void StopQuery()
    {
        if (!IsQueryRunning)
        {
            return;
        }

        _queryCts?.Cancel();
        _pauseEvent?.Set();
        IsQueryPaused = false;
        StatusMessage = "Canceling query...";
    }

    private void ClearQuery()
    {
        StopQuery();
        ClearFilters();
        ResetSorting();
        InitializeQueryResults(_currentColumns);
        StatusMessage = "Ready";
    }

    private void ClearFilters()
    {
        foreach (var filter in _filters)
        {
            filter.Value = string.Empty;
            filter.SortState = ColumnSortState.None;
            filter.SortIndex = 0;
        }

        _sortedFilters.Clear();
        _columnOrder.Clear();
    }

    public override bool OnClosing()
    {
        StopQuery();
        _settingsService.SettingsChanged -= OnSettingsChanged;
        return true;
    }

    public void UpdateColumnOrder(IEnumerable<string> orderedColumns)
    {
        _columnOrder.Clear();
        foreach (var column in orderedColumns)
        {
            if (!string.IsNullOrWhiteSpace(column))
            {
                _columnOrder.Add(column);
            }
        }

        UpdateViewportColumns(_currentColumns);
    }

    public void MoveColumnToIndex(string columnName, int targetIndex)
    {
        var ordered = GetOrderedColumns(_currentColumns).ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        int sourceIndex = FindColumnIndex(ordered, columnName);
        if (sourceIndex < 0)
        {
            return;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, ordered.Count);
        if (sourceIndex < frozenCount)
        {
            targetIndex = Math.Clamp(targetIndex, 0, Math.Max(0, frozenCount - 1));
        }
        else
        {
            targetIndex = Math.Clamp(targetIndex, frozenCount, ordered.Count - 1);
        }

        MoveColumn(ordered, sourceIndex, targetIndex);
        UpdateColumnOrder(ordered);
    }

    public void SendColumnToStart(string columnName)
    {
        var ordered = GetOrderedColumns(_currentColumns).ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, ordered.Count);
        int index = FindColumnIndex(ordered, columnName);
        if (index < 0 || index < frozenCount)
        {
            return;
        }

        int targetIndex = frozenCount > 0 ? frozenCount : 0;
        MoveColumn(ordered, index, targetIndex);
        UpdateColumnOrder(ordered);
    }

    public void LockColumn(string columnName)
    {
        var ordered = GetOrderedColumns(_currentColumns).ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, ordered.Count);
        int index = FindColumnIndex(ordered, columnName);
        if (index < 0 || index < frozenCount)
        {
            return;
        }

        MoveColumn(ordered, index, frozenCount);
        UpdateColumnOrder(ordered);
        FrozenColumnCount = Math.Min(ordered.Count, frozenCount + 1);
    }

    public void UnlockColumn(string columnName)
    {
        var ordered = GetOrderedColumns(_currentColumns).ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, ordered.Count);
        int index = FindColumnIndex(ordered, columnName);
        if (index < 0 || index >= frozenCount || frozenCount <= 0)
        {
            return;
        }

        int targetIndex = frozenCount - 1;
        MoveColumn(ordered, index, targetIndex);
        UpdateColumnOrder(ordered);
        FrozenColumnCount = Math.Max(0, frozenCount - 1);
    }

    private static int FindColumnIndex(IReadOnlyList<string> columns, string columnName)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void MoveColumn(List<string> columns, int sourceIndex, int targetIndex)
    {
        if (sourceIndex == targetIndex)
        {
            return;
        }

        if (sourceIndex < 0 || sourceIndex >= columns.Count)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, columns.Count - 1);
        string item = columns[sourceIndex];
        columns.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        columns.Insert(targetIndex, item);
    }

    public IReadOnlyList<string> GetOrderedColumns(IReadOnlyList<string> availableColumns)
    {
        if (availableColumns.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (_columnOrder.Count == 0)
        {
            foreach (var column in availableColumns)
            {
                if (!string.IsNullOrWhiteSpace(column))
                {
                    _columnOrder.Add(column);
                }
            }
        }

        var availableSet = new HashSet<string>(availableColumns, StringComparer.OrdinalIgnoreCase);
        _columnOrder.RemoveAll(name => !availableSet.Contains(name));

        foreach (var column in availableColumns)
        {
            if (!string.IsNullOrWhiteSpace(column) && !_columnOrder.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                _columnOrder.Add(column);
            }
        }

        return _columnOrder
            .Where(name => availableSet.Contains(name))
            .ToList();
    }

    public ColumnSortState ToggleSort(string columnName)
    {
        var filter = _filters.FirstOrDefault(f =>
            string.Equals(f.Name, columnName, StringComparison.OrdinalIgnoreCase));
        if (filter == null)
        {
            return ColumnSortState.None;
        }

        if (!_sortedFilters.Contains(filter))
        {
            _sortedFilters.Add(filter);
        }

        filter.SortState = filter.SortState switch
        {
            ColumnSortState.None => ColumnSortState.Ascending,
            ColumnSortState.Ascending => ColumnSortState.Descending,
            _ => ColumnSortState.None
        };

        if (filter.SortState == ColumnSortState.None)
        {
            _sortedFilters.Remove(filter);
            filter.SortIndex = 0;
        }

        UpdateSortIndexes();
        return filter.SortState;
    }

    public void SetViewportColumnWidth(string columnName, double width)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return;
        }

        width = Math.Max(40, width);
        _columnWidthOverrides[columnName] = width;
        UpdateViewportColumns(_currentColumns);
    }

    private void ResetSorting()
    {
        _sortedFilters.Clear();
        foreach (var filter in _filters)
        {
            filter.SortState = ColumnSortState.None;
            filter.SortIndex = 0;
        }
    }

    private void UpdateSortIndexes()
    {
        for (int i = 0; i < _sortedFilters.Count; i++)
        {
            _sortedFilters[i].SortIndex = i + 1;
        }
    }

    private IReadOnlyList<JdeSort> BuildSorts()
    {
        if (_sortedFilters.Count == 0)
        {
            return Array.Empty<JdeSort>();
        }

        return _sortedFilters
            .Where(filter => filter.SortState != ColumnSortState.None)
            .OrderBy(filter => filter.SortIndex)
            .Select(filter => new JdeSort
            {
                ColumnName = filter.Name,
                Direction = filter.SortState == ColumnSortState.Descending
                    ? JdeSortDirection.Descending
                    : JdeSortDirection.Ascending
            })
            .ToList();
    }

    private static bool TryParseFilterInput(string input, out JdeFilterOperator op, out string value)
    {
        op = JdeFilterOperator.Equals;
        value = input?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith(">=", StringComparison.Ordinal))
        {
            op = JdeFilterOperator.GreaterThanOrEqual;
            value = value.Substring(2).Trim();
        }
        else if (value.StartsWith("<=", StringComparison.Ordinal))
        {
            op = JdeFilterOperator.LessThanOrEqual;
            value = value.Substring(2).Trim();
        }
        else if (value.StartsWith("!=", StringComparison.Ordinal))
        {
            op = JdeFilterOperator.NotEquals;
            value = value.Substring(2).Trim();
        }
        else if (value.StartsWith(">", StringComparison.Ordinal))
        {
            op = JdeFilterOperator.GreaterThan;
            value = value.Substring(1).Trim();
        }
        else if (value.StartsWith("<", StringComparison.Ordinal))
        {
            op = JdeFilterOperator.LessThan;
            value = value.Substring(1).Trim();
        }
        else if (value.StartsWith("=", StringComparison.Ordinal))
        {
            op = JdeFilterOperator.Equals;
            value = value.Substring(1).Trim();
        }

        if (op == JdeFilterOperator.Equals && value.Contains('*', StringComparison.Ordinal))
        {
            op = JdeFilterOperator.Like;
            value = value.Replace('*', '%');
        }

        return !string.IsNullOrWhiteSpace(value);
    }

    private void ApplyIndexKeyHighlights()
    {
        var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (SelectedIndex?.KeyColumns != null)
        {
            foreach (var key in SelectedIndex.KeyColumns)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keySet.Add(key);
                }
            }
        }

        foreach (var filter in _filters)
        {
            filter.IsIndexKey = keySet.Contains(filter.Name);
        }
    }

    private void UpdateHeaderDisplay()
    {
        var mode = _settingsService.Current.ColumnHeaderDisplayMode;
        foreach (var filter in _filters)
        {
            if (_isBusinessView)
            {
                BuildViewHeaderParts(
                    filter,
                    mode,
                    _settingsService.Current.ShowTablePrefixInHeader,
                    out string title,
                    out string description);
                ApplyHeaderParts(filter, title, description, mode);
            }
            else
            {
                string prefix = _settingsService.Current.ShowTablePrefixInHeader ? $"{TableName}. " : string.Empty;
                BuildTableHeaderParts(filter, mode, prefix, out string title, out string description);
                ApplyHeaderParts(filter, title, description, mode);
            }
        }

        bool withDescription = mode == ColumnHeaderDisplayMode.DatabaseColumnNameWithDescription
            || mode == ColumnHeaderDisplayMode.DatabaseDataDictionaryWithDescription;
        HeaderRowHeight = withDescription ? HeaderRowHeightWithDescription : HeaderRowHeightSingleLine;
        HeaderTotalHeight = HeaderRowHeight + HeaderFilterRowHeight;

        UpdateViewportColumns(_currentColumns);
    }

    private string BuildColumnWithDatabaseNameDescription(ColumnFilter filter, string prefix)
    {
        string baseName = $"{prefix}{FormatSqlName(filter)}";
        if (string.IsNullOrWhiteSpace(filter.Description))
        {
            return baseName;
        }

        return $"{baseName}{Environment.NewLine}{filter.Description}";
    }
    
    private string BuildColumnWithDataDictionaryDescription(ColumnFilter filter, string prefix)
    {
        string baseName = $"{prefix}{filter.Name}";
        if (string.IsNullOrWhiteSpace(filter.Description))
        {
            return baseName;
        }

        return $"{baseName}{Environment.NewLine}{filter.Description}";
    }

    private static string FormatSqlName(ColumnFilter filter)
    {
        if (filter.Name.Contains('.', StringComparison.Ordinal))
        {
            return filter.Name;
        }

        if (string.IsNullOrWhiteSpace(filter.SqlName))
        {
            return filter.Name;
        }

        string sqlName = filter.SqlName;
        if (string.Equals(sqlName, filter.Name, StringComparison.OrdinalIgnoreCase))
        {
            return filter.Name;
        }

        if (sqlName.Length > 2 && !sqlName.Contains('|', StringComparison.Ordinal))
        {
            return $"{sqlName.Substring(0, 2)}|{sqlName.Substring(2)}";
        }

        return sqlName;
    }

    private static void BuildViewHeaderParts(
        ColumnFilter filter,
        ColumnHeaderDisplayMode mode,
        bool showTablePrefix,
        out string title,
        out string description)
    {
        string tableName = ExtractViewTableName(filter.Name);
        string prefix = showTablePrefix && !string.IsNullOrWhiteSpace(tableName) ? $"{tableName}." : string.Empty;
        string baseName = mode switch
        {
            ColumnHeaderDisplayMode.DatabaseColumnName => FormatViewSqlName(filter),
            ColumnHeaderDisplayMode.DataDictionary => filter.DataItem,
            ColumnHeaderDisplayMode.DatabaseColumnNameWithDescription => FormatViewSqlName(filter),
            ColumnHeaderDisplayMode.DatabaseDataDictionaryWithDescription => filter.DataItem,
            _ => filter.DataItem
        };

        title = $"{prefix}{baseName}";
        if (mode == ColumnHeaderDisplayMode.DatabaseColumnNameWithDescription
            || mode == ColumnHeaderDisplayMode.DatabaseDataDictionaryWithDescription)
        {
            description = filter.Description ?? string.Empty;
            return;
        }

        description = string.Empty;
    }

    private static void BuildTableHeaderParts(
        ColumnFilter filter,
        ColumnHeaderDisplayMode mode,
        string prefix,
        out string title,
        out string description)
    {
        description = string.Empty;
        switch (mode)
        {
            case ColumnHeaderDisplayMode.DatabaseColumnName:
                title = $"{prefix}{FormatSqlName(filter)}";
                return;
            case ColumnHeaderDisplayMode.DataDictionary:
                title = $"{prefix}{filter.Name}";
                return;
            case ColumnHeaderDisplayMode.DatabaseColumnNameWithDescription:
                title = $"{prefix}{FormatSqlName(filter)}";
                description = filter.Description ?? string.Empty;
                return;
            case ColumnHeaderDisplayMode.DatabaseDataDictionaryWithDescription:
                title = $"{prefix}{filter.Name}";
                description = filter.Description ?? string.Empty;
                return;
            default:
                title = $"{prefix}{filter.Name}";
                return;
        }
    }

    private static void ApplyHeaderParts(
        ColumnFilter filter,
        string title,
        string description,
        ColumnHeaderDisplayMode mode)
    {
        filter.HeaderTitle = title;
        filter.HeaderDescription = description;
        bool showDescription = mode == ColumnHeaderDisplayMode.DatabaseColumnNameWithDescription
            || mode == ColumnHeaderDisplayMode.DatabaseDataDictionaryWithDescription;
        filter.ShowHeaderDescription = showDescription && !string.IsNullOrWhiteSpace(description);
        filter.HeaderText = string.IsNullOrWhiteSpace(description)
            ? title
            : $"{title}{Environment.NewLine}{description}";
    }

    private static string FormatViewSqlName(ColumnFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.SqlName))
        {
            return filter.DataItem;
        }

        string sqlName = filter.SqlName;
        if (string.Equals(sqlName, filter.DataItem, StringComparison.OrdinalIgnoreCase))
        {
            return filter.DataItem;
        }

        if (sqlName.Length > 2 && !sqlName.Contains('|', StringComparison.Ordinal))
        {
            return $"{sqlName.Substring(0, 2)}|{sqlName.Substring(2)}";
        }

        return sqlName;
    }

    private static string ExtractViewTableName(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return string.Empty;
        }

        int index = columnName.IndexOf('.', StringComparison.Ordinal);
        if (index > 0)
        {
            return columnName.Substring(0, index);
        }

        return string.Empty;
    }

    private async Task LoadDataDictionaryDescriptionsAsync(CancellationToken cancellationToken)
    {
        var missing = _filters
            .Select(filter => filter.DataItem)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !DataDictionaryCache.ContainsKey(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count == 0)
        {
            ApplyDescriptionsFromCache();
            return;
        }

        try
        {
            var titles = await _connectionService.RunExclusiveAsync(
                client => client.GetDataDictionaryTitlesAsync(missing, cancellationToken),
                cancellationToken);

            foreach (var title in titles)
            {
                if (!string.IsNullOrWhiteSpace(title.DataItem))
                {
                    DataDictionaryCache[title.DataItem] = title;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to load data dictionary titles for query filters.");
        }

        ApplyDescriptionsFromCache();
    }

    private void ApplyDescriptionsFromCache()
    {
        foreach (var filter in _filters)
        {
            if (DataDictionaryCache.TryGetValue(filter.DataItem, out var title))
            {
                filter.Description = BuildDescription(title);
            }
        }
    }

    private static string? BuildDescription(JdeDataDictionaryTitle title)
    {
        string part1 = title.Title1?.Trim() ?? string.Empty;
        string part2 = title.Title2?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(part1) && string.IsNullOrWhiteSpace(part2))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(part1))
        {
            return part2;
        }

        if (string.IsNullOrWhiteSpace(part2))
        {
            return part1;
        }

        return $"{part1}{Environment.NewLine}{part2}";
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        UpdateHeaderDisplay();
        var width = _settingsService.Current.QueryColumnWidth;
        if (width > 0 && !QueryColumnWidth.Equals(width))
        {
            QueryColumnWidth = width;
        }

        UpdateViewportColumns(_currentColumns);
    }
}
