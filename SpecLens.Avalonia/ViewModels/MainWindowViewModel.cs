using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using Avalonia.Threading;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Models;
using SpecLens.Avalonia.Models;
using SpecLens.Avalonia.Services;
using Serilog;

namespace SpecLens.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly IJdeConnectionService _connectionService;
    private readonly IAppSettingsService _settingsService;
    private readonly IDataDictionaryInfoService _dataDictionaryInfoService;
    private readonly ObservableCollection<JdeObjectInfo> _objectResults = new();
    private string _title = "Spec Lens";
    private string _statusMessage = "Not connected";
    private bool _isConnected;
    private bool _isConnecting;
    private bool _isLocked = true;
    private WorkspaceTabViewModel? _selectedTab;
    private string _searchText = string.Empty;
    private string _descriptionSearchText = string.Empty;
    private JdeObjectType _selectedObjectType;
    private ObjectTypeOption? _selectedObjectTypeOption;
    private JdeObjectInfo? _selectedObject;
    private bool _isSearching;

    public MainWindowViewModel(
        IJdeConnectionService connectionService,
        IAppSettingsService settingsService,
        IDataDictionaryInfoService dataDictionaryInfoService)
    {
        _connectionService = connectionService;
        _settingsService = settingsService;
        _dataDictionaryInfoService = dataDictionaryInfoService;
        ObjectResults = new ReadOnlyObservableCollection<JdeObjectInfo>(_objectResults);
        ObjectTypeOptions = BuildObjectTypeOptions();

        SearchText = _settingsService.Current.SearchText;
        DescriptionSearchText = _settingsService.Current.DescriptionSearchText;
        SelectedObjectType = _settingsService.Current.ObjectTypeFilter;
        IsConnected = _connectionService.IsConnected;
        IsConnecting = _connectionService.IsConnecting;
        SelectedObjectTypeOption = ObjectTypeOptions.FirstOrDefault(option => option.Value == SelectedObjectType)
            ?? ObjectTypeOptions.FirstOrDefault();
        StatusMessage = _connectionService.StatusMessage;

        var canConnect = this.WhenAnyValue(
            x => x.IsConnected,
            x => x.IsConnecting,
            (connected, connecting) => !connected && !connecting);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);

        var canDisconnect = this.WhenAnyValue(x => x.IsConnected);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, canDisconnect);

        var canSearch = this.WhenAnyValue(
            x => x.IsConnected,
            x => x.IsSearching,
            (connected, searching) => connected && !searching);
        SearchCommand = ReactiveCommand.CreateFromTask(SearchAsync, canSearch);

        var canOpenSpecs = this.WhenAnyValue(x => x.IsConnected, x => x.HasSelection,
            (connected, hasSelection) => connected && hasSelection);
        OpenSpecsCommand = ReactiveCommand.CreateFromTask(OpenSpecsAsync, canOpenSpecs);

        var canOpenQuery = this.WhenAnyValue(x => x.IsConnected, x => x.SelectedObject,
            (connected, selected) => connected && selected != null && IsQueryable(selected));
        OpenQueryCommand = ReactiveCommand.CreateFromTask(OpenQueryAsync, canOpenQuery);

        var canOpenEventRules = this.WhenAnyValue(x => x.IsConnected, x => x.SelectedObject,
            (connected, selected) => connected && selected != null);
        OpenEventRulesCommand = ReactiveCommand.CreateFromTask(OpenEventRulesAsync, canOpenEventRules);

        CloseTabCommand = ReactiveCommand.Create<WorkspaceTabViewModel?>(CloseTab);
        CloseAllTabsCommand = ReactiveCommand.Create(CloseAllTabs);

        var canOpenSpecsForObject = this.WhenAnyValue(x => x.IsConnected);
        OpenSpecsForObjectCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenSpecsForObjectAsync, canOpenSpecsForObject);
        OpenQueryForObjectCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenQueryForObjectAsync, canOpenSpecsForObject);
        OpenEventRulesForObjectCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenEventRulesForObjectAsync, canOpenSpecsForObject);
        DefaultOpenCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenDefaultAsync, canOpenSpecsForObject);

        this.WhenActivated(disposables =>
        {
            _connectionService.WhenAnyValue(x => x.StatusMessage)
                .Subscribe(message => StatusMessage = message)
                .DisposeWith(disposables);

            _connectionService.WhenAnyValue(x => x.IsConnected, x => x.IsConnecting)
                .Subscribe(tuple =>
                {
                    IsConnected = tuple.Item1;
                    IsConnecting = tuple.Item2;
                    UpdateConnectionState();
                })
                .DisposeWith(disposables);

            Observable.FromEventPattern<EventHandler, EventArgs>(
                    h => _settingsService.SettingsChanged += h,
                    h => _settingsService.SettingsChanged -= h)
                .Subscribe(_ => OnSettingsChanged())
                .DisposeWith(disposables);

            Observable.FromAsync(ConnectAsync)
                .Subscribe(_ => { })
                .DisposeWith(disposables);
        });

        UpdateConnectionState();
    }

    public ViewModelActivator Activator { get; } = new();

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => this.RaiseAndSetIfChanged(ref _isLocked, value);
    }

    public WorkspaceTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value)
            {
                return;
            }

            var previous = _selectedTab;
            this.RaiseAndSetIfChanged(ref _selectedTab, value);

            if (previous != null)
            {
                previous.IsActive = false;
                previous.OnDeactivated();
            }

            if (value != null)
            {
                value.IsActive = true;
                value.OnActivated();
            }
        }
    }

    public ObservableCollection<WorkspaceTabViewModel> Tabs { get; } = new();

    public ReadOnlyObservableCollection<JdeObjectInfo> ObjectResults { get; }
    public IReadOnlyList<ObjectTypeOption> ObjectTypeOptions { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (string.Equals(_searchText, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _searchText, value);
            _settingsService.Current.SearchText = value;
            _settingsService.Save();
        }
    }

    public string DescriptionSearchText
    {
        get => _descriptionSearchText;
        set
        {
            if (string.Equals(_descriptionSearchText, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _descriptionSearchText, value);
            _settingsService.Current.DescriptionSearchText = value;
            _settingsService.Save();
        }
    }

    public JdeObjectType SelectedObjectType
    {
        get => _selectedObjectType;
        set
        {
            if (_selectedObjectType == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedObjectType, value);
            _settingsService.Update(settings => settings.ObjectTypeFilter = value);
            if (SelectedObjectTypeOption?.Value != value)
            {
                SelectedObjectTypeOption = ObjectTypeOptions.FirstOrDefault(option => option.Value == value)
                    ?? ObjectTypeOptions.FirstOrDefault();
            }
        }
    }

    public ObjectTypeOption? SelectedObjectTypeOption
    {
        get => _selectedObjectTypeOption;
        set
        {
            if (EqualityComparer<ObjectTypeOption?>.Default.Equals(_selectedObjectTypeOption, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedObjectTypeOption, value);
            if (value != null && value.Value != SelectedObjectType)
            {
                SelectedObjectType = value.Value;
            }
        }
    }

    public JdeObjectInfo? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (EqualityComparer<JdeObjectInfo?>.Default.Equals(_selectedObject, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedObject, value);
            this.RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => this.RaiseAndSetIfChanged(ref _isSearching, value);
    }

    public bool HasSelection => SelectedObject != null;

    public IDataDictionaryInfoService DataDictionaryInfo => _dataDictionaryInfoService;

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSpecsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenQueryCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenEventRulesCommand { get; }
    public ReactiveCommand<WorkspaceTabViewModel?, Unit> CloseTabCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseAllTabsCommand { get; }
    public ReactiveCommand<JdeObjectInfo?, Unit> OpenSpecsForObjectCommand { get; }
    public ReactiveCommand<JdeObjectInfo?, Unit> OpenQueryForObjectCommand { get; }
    public ReactiveCommand<JdeObjectInfo?, Unit> OpenEventRulesForObjectCommand { get; }
    public ReactiveCommand<JdeObjectInfo?, Unit> DefaultOpenCommand { get; }

    private async Task ConnectAsync()
    {
        try
        {
            await _connectionService.ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = ex.Message);
            Log.Error(ex, "Failed to connect to JDE");
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _connectionService.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = ex.Message);
            Log.Error(ex, "Failed to disconnect from JDE");
        }
    }

    private async Task OpenQueryTabAsync()
    {
        await OpenQueryForObjectAsync(SelectedObject);
    }

    private async Task OpenSpecsTabAsync()
    {
        await OpenSpecsForObjectAsync(SelectedObject);
    }

    private async Task OpenEventRulesTabAsync()
    {
        await OpenEventRulesForObjectAsync(SelectedObject);
    }

    private void CloseTab(WorkspaceTabViewModel? tab)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => CloseTab(tab));
            return;
        }

        if (tab == null)
            return;

        if (!tab.OnClosing())
            return;

        var index = Tabs.IndexOf(tab);
        bool wasSelected = SelectedTab == tab;

        Tabs.Remove(tab);

        if (wasSelected)
        {
            if (Tabs.Count == 0)
            {
                SelectedTab = null;
                return;
            }

            if (index >= Tabs.Count)
            {
                index = Tabs.Count - 1;
            }

            SelectedTab = Tabs[index];
        }
    }

    private void CloseAllTabs()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(CloseAllTabs);
            return;
        }

        if (Tabs.Count == 0)
        {
            return;
        }

        var tabs = Tabs.ToList();
        bool closedSelected = false;
        foreach (var tab in tabs)
        {
            if (!tab.OnClosing())
            {
                continue;
            }

            if (SelectedTab == tab)
            {
                closedSelected = true;
            }

            Tabs.Remove(tab);
        }

        if (closedSelected)
        {
            SelectedTab = Tabs.Count > 0 ? Tabs.Last() : null;
        }
    }

    private async Task SearchAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            IsSearching = true;
            StatusMessage = "Searching objects...";
        });

        try
        {
            string namePattern = SearchText.Trim();
            string descriptionPattern = DescriptionSearchText.Trim();
            var results = await _connectionService.RunExclusiveAsync(
                client => client.GetObjectsAsync(
                    SelectedObjectType,
                    searchPattern: namePattern,
                    descriptionPattern: descriptionPattern,
                    maxResults: 50000));

            await RunOnUiThreadAsync(() =>
            {
                _objectResults.Clear();
                foreach (var item in results)
                {
                    _objectResults.Add(item);
                }

                StatusMessage = $"Found {results.Count} objects";
            });
        }
        catch (JdeConnectionException ex)
        {
            await HandleConnectionLossAsync(ex.Message).ConfigureAwait(false);
            Log.Warning(ex, "Connection lost during object search");
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = ex.Message);
            Log.Error(ex, "Object search failed");
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsSearching = false);
        }
    }

    private Task OpenSpecsAsync()
    {
        return OpenSpecsForObjectAsync(SelectedObject);
    }

    private Task OpenQueryAsync()
    {
        return OpenQueryForObjectAsync(SelectedObject);
    }

    private Task OpenEventRulesAsync()
    {
        return OpenEventRulesForObjectAsync(SelectedObject);
    }

    private Task OpenSpecsForObjectAsync(JdeObjectInfo? jdeObject)
    {
        if (jdeObject == null)
        {
            return Task.CompletedTask;
        }

        return RunOnUiThreadAsync(() =>
        {
            var tab = new SpecsTabViewModel(jdeObject, _connectionService)
            {
                Header = $"{jdeObject.ObjectName} - Specs"
            };

            Tabs.Add(tab);
            SelectedTab = tab;
            _ = tab.LoadAsync();
        });
    }

    public async Task OpenSpecsForObjectNameAsync(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        var resolved = await ResolveSpecObjectAsync(objectName);
        if (resolved == null)
        {
            StatusMessage = $"Object '{objectName}' was not found in the catalog.";
            return;
        }

        await OpenSpecsForObjectAsync(resolved);
    }

    private Task OpenQueryForObjectAsync(JdeObjectInfo? jdeObject)
    {
        if (jdeObject == null || !IsQueryable(jdeObject))
        {
            return Task.CompletedTask;
        }

        return RunOnUiThreadAsync(() =>
        {
            bool isBusinessView = string.Equals(jdeObject.ObjectType?.Trim(), "BSVW", StringComparison.OrdinalIgnoreCase);
            var tab = new QueryTabViewModel(jdeObject.ObjectName, _connectionService, _settingsService, isBusinessView)
            {
                Header = $"{jdeObject.ObjectName} - Query"
            };

            Tabs.Add(tab);
            SelectedTab = tab;
            _ = tab.InitializeAsync();
        });
    }

    private Task OpenEventRulesForObjectAsync(JdeObjectInfo? jdeObject)
    {
        return OpenEventRulesForObjectAsync(jdeObject, initialFunctionName: null);
    }

    private Task OpenEventRulesForObjectAsync(
        JdeObjectInfo? jdeObject,
        string? initialFunctionName)
    {
        if (jdeObject == null)
        {
            return Task.CompletedTask;
        }

        return RunOnUiThreadAsync(() =>
        {
            var tab = new EventRulesTabViewModel(jdeObject, _connectionService, initialFunctionName)
            {
                Header = $"{jdeObject.ObjectName} - Event Rules"
            };

            Tabs.Add(tab);
            SelectedTab = tab;
            _ = tab.LoadAsync();
        });
    }

    public Task OpenEventRulesForFunctionAsync(string objectName, string functionName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return Task.CompletedTask;
        }

        var jdeObject = new JdeObjectInfo
        {
            ObjectName = objectName,
            ObjectType = "NER"
        };

        return OpenEventRulesForObjectAsync(jdeObject, functionName);
    }

    private Task OpenDefaultAsync(JdeObjectInfo? jdeObject)
    {
        if (jdeObject == null)
        {
            return Task.CompletedTask;
        }

        return OpenSpecsForObjectAsync(jdeObject);
    }

    private static bool IsQueryable(JdeObjectInfo jdeObject)
    {
        string? type = jdeObject.ObjectType?.Trim();
        return string.Equals(type, "TBLE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "BSVW", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JdeObjectInfo?> ResolveSpecObjectAsync(string objectName)
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected";
            return null;
        }

        string trimmed = objectName.Trim();
        try
        {
            return await _connectionService.RunExclusiveAsync(async client =>
            {
                var viewMatches = await client.GetObjectsAsync(JdeObjectType.BusinessView, trimmed, maxResults: 1);
                var view = viewMatches.FirstOrDefault(match =>
                    string.Equals(match.ObjectName, trimmed, StringComparison.OrdinalIgnoreCase));
                if (view != null)
                {
                    return view;
                }

                var tableMatches = await client.GetObjectsAsync(JdeObjectType.Table, trimmed, maxResults: 1);
                return tableMatches.FirstOrDefault(match =>
                    string.Equals(match.ObjectName, trimmed, StringComparison.OrdinalIgnoreCase));
            });
        }
        catch (JdeConnectionException ex)
        {
            StatusMessage = ex.Message;
            Log.Warning(ex, "Failed to resolve object {ObjectName}", trimmed);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to resolve {trimmed}.";
            Log.Error(ex, "Failed to resolve object {ObjectName}", trimmed);
        }

        return null;
    }

    private async Task HandleConnectionLossAsync(string message)
    {
        await RunOnUiThreadAsync(() => StatusMessage = message);
        await _connectionService.MarkDisconnectedAsync(message).ConfigureAwait(false);
        UpdateConnectionState();
    }

    private void UpdateConnectionState()
    {
        RunOnUiThread(() => IsLocked = !_connectionService.IsConnected || _connectionService.IsConnecting);
    }

    private void OnSettingsChanged()
    {
        RunOnUiThread(() =>
        {
            var mode = _settingsService.Current.ObjectTypeFilter;
            if (SelectedObjectType != mode)
            {
                SelectedObjectType = mode;
            }

            var search = _settingsService.Current.SearchText;
            if (!string.Equals(SearchText, search, StringComparison.Ordinal))
            {
                SearchText = search;
            }

            var description = _settingsService.Current.DescriptionSearchText;
            if (!string.Equals(DescriptionSearchText, description, StringComparison.Ordinal))
            {
                DescriptionSearchText = description;
            }
        });
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private static IReadOnlyList<ObjectTypeOption> BuildObjectTypeOptions()
    {
        return new List<ObjectTypeOption>
        {
            new("All", JdeObjectType.All),
            new("Table (TBLE)", JdeObjectType.Table),
            new("Business Function (BSFN)", JdeObjectType.BusinessFunction),
            new("Named Event Rule (NER)", JdeObjectType.NamedEventRule),
            new("Report (UBE)", JdeObjectType.Report),
            new("Application (APPL)", JdeObjectType.Application),
            new("Data Structure (DSTR)", JdeObjectType.DataStructure),
            new("Business View (BSVW)", JdeObjectType.BusinessView),
            new("Data Dictionary (DD)", JdeObjectType.DataDictionary)
        };
    }
}
