using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
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
    [Flags]
    private enum ObjectAction
    {
        None = 0,
        Specs = 1,
        Query = 2,
        EventRules = 4
    }

    private const ObjectAction DefaultObjectActions = ObjectAction.Specs | ObjectAction.Query;
    private static readonly ObjectAction[] DefaultActionOrder =
    {
        ObjectAction.Specs,
        ObjectAction.Query,
        ObjectAction.EventRules
    };

    private static readonly IReadOnlyDictionary<string, ObjectAction> ObjectTypeActions
        = new Dictionary<string, ObjectAction>(StringComparer.OrdinalIgnoreCase)
        {
            ["TBLE"] = DefaultObjectActions,
            ["BSVW"] = DefaultObjectActions,
            ["BSFN"] = ObjectAction.EventRules,
            ["UBE"] = ObjectAction.EventRules,
            ["APPL"] = ObjectAction.None,
            ["DSTR"] = ObjectAction.None,
        };

    private readonly IJdeConnectionService _connectionService;
    private readonly IAppSettingsService _settingsService;
    private readonly IDataDictionaryInfoService _dataDictionaryInfoService;
    private readonly ObservableCollection<JdeObjectInfo> _objectResults = new();
    private readonly ObservableCollection<ObjectLocationOption> _objectLocationOptions = new();
    private readonly ReadOnlyObservableCollection<ObjectLocationOption> _readOnlyObjectLocationOptions;
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
    private ObjectLocationOption? _selectedObjectLocationOption;
    private ObjectLocationOption _lastSearchLocation = ObjectLocationOption.Local;
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
        _readOnlyObjectLocationOptions = new ReadOnlyObservableCollection<ObjectLocationOption>(_objectLocationOptions);
        ObjectTypeOptions = BuildObjectTypeOptions();
        ApplyObjectLocationOptions(Array.Empty<string>(), _settingsService.Current.ObjectSearchPathCode);

        SearchText = _settingsService.Current.SearchText;
        DescriptionSearchText = _settingsService.Current.DescriptionSearchText;
        SelectedObjectType = _settingsService.Current.ObjectTypeFilter;
        IsConnected = _connectionService.IsConnected;
        IsConnecting = _connectionService.IsConnecting;
        SelectedObjectTypeOption = ObjectTypeOptions.FirstOrDefault(option => option.Value == SelectedObjectType)
            ?? ObjectTypeOptions.FirstOrDefault();
        SelectedObjectLocationOption = ResolveLocationOption(_settingsService.Current.ObjectSearchPathCode);
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

        var canOpenSpecs = this.WhenAnyValue(x => x.IsConnected, x => x.SelectedObject,
            (connected, selected) => connected && IsActionAllowed(selected, ObjectAction.Specs));
        OpenSpecsCommand = ReactiveCommand.CreateFromTask(OpenSpecsAsync, canOpenSpecs);

        var canOpenQuery = this.WhenAnyValue(x => x.IsConnected, x => x.SelectedObject,
            (connected, selected) => connected && IsActionAllowed(selected, ObjectAction.Query));
        OpenQueryCommand = ReactiveCommand.CreateFromTask(OpenQueryAsync, canOpenQuery);

        var canOpenEventRules = this.WhenAnyValue(x => x.IsConnected, x => x.SelectedObject,
            (connected, selected) => connected && IsActionAllowed(selected, ObjectAction.EventRules));
        OpenEventRulesCommand = ReactiveCommand.CreateFromTask(OpenEventRulesAsync, canOpenEventRules);

        CloseTabCommand = ReactiveCommand.Create<WorkspaceTabViewModel?>(CloseTab);
        CloseAllTabsCommand = ReactiveCommand.Create(CloseAllTabs);

        var canOpenSpecsForObject = this.WhenAnyValue(x => x.IsConnected);
        OpenSpecsForObjectCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenSpecsForObjectAsync, canOpenSpecsForObject);
        OpenQueryForObjectCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenQueryForObjectAsync, canOpenSpecsForObject);
        OpenEventRulesForObjectCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenEventRulesForObjectAsync, canOpenSpecsForObject);
        var canDefaultOpen = this.WhenAnyValue(x => x.IsConnected, x => x.SelectedObject,
            (connected, selected) => connected && TryGetDefaultAction(selected, out _));
        DefaultOpenCommand = ReactiveCommand.CreateFromTask<JdeObjectInfo?>(OpenDefaultAsync, canDefaultOpen);

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
    public ReadOnlyObservableCollection<ObjectLocationOption> ObjectLocationOptions => _readOnlyObjectLocationOptions;

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

    public ObjectLocationOption? SelectedObjectLocationOption
    {
        get => _selectedObjectLocationOption;
        set
        {
            if (MatchesLocation(_selectedObjectLocationOption, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedObjectLocationOption, value);
            string pathCode = value?.PathCode ?? string.Empty;
            if (!string.Equals(_settingsService.Current.ObjectSearchPathCode, pathCode, StringComparison.OrdinalIgnoreCase))
            {
                _settingsService.Update(settings => settings.ObjectSearchPathCode = pathCode);
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

    public IAppSettingsService SettingsService => _settingsService;

    private async Task ConnectAsync()
    {
        try
        {
            await _connectionService.ConnectAsync().ConfigureAwait(false);
            await LoadObjectLocationOptionsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = ex.Message);
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
            var location = SelectedObjectLocationOption ?? ObjectLocationOption.Local;
            string? dataSourceOverride = location.ObjectLibrarianDataSourceOverride;
            bool allowDataSourceFallback = location.IsLocal;
            var results = await _connectionService.RunExclusiveAsync(
                client => client.GetObjectsAsync(
                    SelectedObjectType,
                    searchPattern: namePattern,
                    descriptionPattern: descriptionPattern,
                    maxResults: 50000,
                    dataSourceOverride: dataSourceOverride,
                    allowDataSourceFallback: allowDataSourceFallback));

            await RunOnUiThreadAsync(() =>
            {
                _lastSearchLocation = location;
                _objectResults.Clear();
                foreach (var item in results)
                {
                    _objectResults.Add(item);
                }

                StatusMessage = $"Found {results.Count} objects in {location.DisplayName}";
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
        return OpenSpecsForObjectAsync(SelectedObject, ResolveLocationOption(SelectedObject));
    }

    private Task OpenQueryAsync()
    {
        return OpenQueryForObjectAsync(SelectedObject, ResolveLocationOption(SelectedObject));
    }

    private Task OpenEventRulesAsync()
    {
        return OpenEventRulesForObjectAsync(SelectedObject, ResolveLocationOption(SelectedObject), initialFunctionName: null);
    }

    private Task OpenSpecsForObjectAsync(JdeObjectInfo? jdeObject)
    {
        return OpenSpecsForObjectAsync(jdeObject, ResolveLocationOption(jdeObject));
    }

    private Task OpenSpecsForObjectAsync(JdeObjectInfo? jdeObject, ObjectLocationOption locationOption)
    {
        if (jdeObject == null || !IsActionAllowed(jdeObject, ObjectAction.Specs))
        {
            return Task.CompletedTask;
        }

        return RunOnUiThreadAsync(() =>
        {
            string locationLabel = locationOption.DisplayName;
            string tabId = BuildSpecsTabId(jdeObject.ObjectName, locationOption);
            if (TrySelectExistingTab(tabId))
            {
                return;
            }

            var tab = new SpecsTabViewModel(
                jdeObject,
                _connectionService,
                _dataDictionaryInfoService,
                locationOption.ObjectLibrarianDataSourceOverride,
                locationLabel)
            {
                Header = $"{jdeObject.ObjectName} [{locationLabel}] - Specs"
            };
            tab.TabId = tabId;

            Tabs.Add(tab);
            SelectedTab = tab;
            _ = tab.LoadAsync();
        });
    }

    public Task OpenSpecsForObjectNameAsync(string objectName)
    {
        return OpenSpecsForObjectNameAsync(objectName, pathCode: null);
    }

    public async Task OpenSpecsForObjectNameAsync(string objectName, string? pathCode)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        var location = ResolveLocationOption(pathCode);
        var resolved = await ResolveSpecObjectAsync(objectName, location);
        if (resolved == null)
        {
            StatusMessage = $"Object '{objectName}' was not found in the catalog.";
            return;
        }

        await OpenSpecsForObjectAsync(resolved, location);
    }

    private Task OpenQueryForObjectAsync(JdeObjectInfo? jdeObject)
    {
        return OpenQueryForObjectAsync(jdeObject, ResolveLocationOption(jdeObject));
    }

    private Task OpenQueryForObjectAsync(JdeObjectInfo? jdeObject, ObjectLocationOption locationOption)
    {
        if (jdeObject == null || !IsActionAllowed(jdeObject, ObjectAction.Query))
        {
            return Task.CompletedTask;
        }

        return RunOnUiThreadAsync(() =>
        {
            bool isBusinessView = string.Equals(jdeObject.ObjectType?.Trim(), "BSVW", StringComparison.OrdinalIgnoreCase);
            string locationLabel = locationOption.DisplayName;
            string tabId = BuildQueryTabId(jdeObject.ObjectName, locationOption, isBusinessView);
            if (TrySelectExistingTab(tabId))
            {
                return;
            }

            var tab = new QueryTabViewModel(
                jdeObject.ObjectName,
                _connectionService,
                _settingsService,
                _dataDictionaryInfoService,
                isBusinessView,
                locationOption.ObjectLibrarianDataSourceOverride,
                locationLabel)
            {
                Header = $"{jdeObject.ObjectName} [{locationLabel}] - Query"
            };
            tab.TabId = tabId;

            Tabs.Add(tab);
            SelectedTab = tab;
            _ = tab.InitializeAsync();
        });
    }

    private Task OpenEventRulesForObjectAsync(JdeObjectInfo? jdeObject)
    {
        return OpenEventRulesForObjectAsync(jdeObject, ResolveLocationOption(jdeObject), initialFunctionName: null);
    }

    private Task OpenEventRulesForObjectAsync(
        JdeObjectInfo? jdeObject,
        ObjectLocationOption locationOption,
        string? initialFunctionName)
    {
        if (jdeObject == null || !IsActionAllowed(jdeObject, ObjectAction.EventRules))
        {
            return Task.CompletedTask;
        }

        return RunOnUiThreadAsync(() =>
        {
            string locationLabel = locationOption.DisplayName;
            string tabId = BuildEventRulesTabId(jdeObject.ObjectName, locationOption);
            if (string.IsNullOrWhiteSpace(initialFunctionName) && TrySelectExistingTab(tabId))
            {
                return;
            }

            var tab = new EventRulesTabViewModel(
                jdeObject,
                _connectionService,
                locationOption,
                initialFunctionName)
            {
                Header = $"{jdeObject.ObjectName} [{locationLabel}] - Event Rules"
            };
            tab.TabId = tabId;

            Tabs.Add(tab);
            SelectedTab = tab;
            _ = tab.LoadAsync();
        });
    }

    public Task OpenEventRulesForFunctionAsync(string objectName, string functionName)
    {
        return OpenEventRulesForFunctionAsync(objectName, functionName, pathCode: null);
    }

    public Task OpenEventRulesForFunctionAsync(string objectName, string functionName, string? pathCode)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return Task.CompletedTask;
        }

        var jdeObject = new JdeObjectInfo
        {
            ObjectName = objectName,
            ObjectType = "BSFN"
        };

        return OpenEventRulesForObjectAsync(jdeObject, ResolveLocationOption(pathCode), functionName);
    }

    private Task OpenDefaultAsync(JdeObjectInfo? jdeObject)
    {
        if (!TryGetDefaultAction(jdeObject, out var action))
        {
            return Task.CompletedTask;
        }

        return action switch
        {
            ObjectAction.Specs => OpenSpecsForObjectAsync(jdeObject),
            ObjectAction.Query => OpenQueryForObjectAsync(jdeObject),
            ObjectAction.EventRules => OpenEventRulesForObjectAsync(jdeObject),
            _ => Task.CompletedTask
        };
    }

    private static bool IsQueryable(JdeObjectInfo jdeObject)
    {
        return IsActionAllowed(jdeObject, ObjectAction.Query);
    }

    private static bool IsActionAllowed(JdeObjectInfo? jdeObject, ObjectAction action)
    {
        if (jdeObject == null)
        {
            return false;
        }

        return (GetObjectActions(jdeObject) & action) != 0;
    }

    private static ObjectAction GetObjectActions(JdeObjectInfo jdeObject)
    {
        string? type = jdeObject.ObjectType?.Trim();
        if (string.IsNullOrWhiteSpace(type))
        {
            return DefaultObjectActions;
        }

        return ObjectTypeActions.TryGetValue(type, out var actions)
            ? actions
            : DefaultObjectActions;
    }

    private static bool TryGetDefaultAction(JdeObjectInfo? jdeObject, out ObjectAction action)
    {
        action = ObjectAction.None;
        if (jdeObject == null)
        {
            return false;
        }

        var available = GetObjectActions(jdeObject);
        foreach (var candidate in DefaultActionOrder)
        {
            if ((available & candidate) != 0)
            {
                action = candidate;
                return true;
            }
        }

        return false;
    }

    private async Task<JdeObjectInfo?> ResolveSpecObjectAsync(string objectName, ObjectLocationOption? locationOption = null)
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected";
            return null;
        }

        string trimmed = objectName.Trim();
        var location = locationOption ?? ResolveLocationOption((string?)null);
        string? dataSourceOverride = location.ObjectLibrarianDataSourceOverride;
        bool allowDataSourceFallback = location.IsLocal;
        try
        {
            return await _connectionService.RunExclusiveAsync(async client =>
            {
                var viewMatches = await client.GetObjectsAsync(
                    JdeObjectType.BusinessView,
                    trimmed,
                    maxResults: 1,
                    dataSourceOverride: dataSourceOverride,
                    allowDataSourceFallback: allowDataSourceFallback);
                var view = viewMatches.FirstOrDefault(match =>
                    string.Equals(match.ObjectName, trimmed, StringComparison.OrdinalIgnoreCase));
                if (view != null)
                {
                    return view;
                }

                var tableMatches = await client.GetObjectsAsync(
                    JdeObjectType.Table,
                    trimmed,
                    maxResults: 1,
                    dataSourceOverride: dataSourceOverride,
                    allowDataSourceFallback: allowDataSourceFallback);
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

    private async Task LoadObjectLocationOptionsAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            var pathCodes = await _connectionService.RunExclusiveAsync(
                client => client.GetAvailablePathCodesAsync());

            await RunOnUiThreadAsync(() =>
            {
                ApplyObjectLocationOptions(pathCodes, _settingsService.Current.ObjectSearchPathCode);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load object search pathcodes");
        }
    }

    private void ApplyObjectLocationOptions(IEnumerable<string>? pathCodes, string? selectedPathCode)
    {
        var options = new List<ObjectLocationOption> { ObjectLocationOption.Local };
        if (pathCodes != null)
        {
            foreach (var pathCode in pathCodes
                         .Where(code => !string.IsNullOrWhiteSpace(code))
                         .Select(code => code.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(code => code, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new ObjectLocationOption(pathCode, pathCode));
            }
        }

        _objectLocationOptions.Clear();
        foreach (var option in options)
        {
            _objectLocationOptions.Add(option);
        }

        ObjectLocationOption resolved;
        if (string.IsNullOrWhiteSpace(selectedPathCode))
        {
            resolved = _objectLocationOptions.FirstOrDefault(option => option.IsLocal) ?? ObjectLocationOption.Local;
        }
        else
        {
            resolved = _objectLocationOptions.FirstOrDefault(option => option.MatchesPathCode(selectedPathCode))
                       ?? ObjectLocationOption.FromPathCode(selectedPathCode);
        }

        if (!_objectLocationOptions.Any(option => option.MatchesPathCode(resolved.PathCode)))
        {
            _objectLocationOptions.Add(resolved);
        }

        SelectedObjectLocationOption = _objectLocationOptions.FirstOrDefault(option => option.MatchesPathCode(resolved.PathCode))
                                       ?? resolved;
    }

    private static bool MatchesLocation(ObjectLocationOption? left, ObjectLocationOption? right)
    {
        string leftCode = left?.PathCode ?? string.Empty;
        string rightCode = right?.PathCode ?? string.Empty;
        return string.Equals(leftCode, rightCode, StringComparison.OrdinalIgnoreCase);
    }

    private ObjectLocationOption ResolveLocationOption(JdeObjectInfo? jdeObject)
    {
        if (jdeObject != null && _objectResults.Contains(jdeObject))
        {
            return _lastSearchLocation;
        }

        return SelectedObjectLocationOption ?? ObjectLocationOption.Local;
    }

    private ObjectLocationOption ResolveLocationOption(string? pathCode)
    {
        if (string.IsNullOrWhiteSpace(pathCode))
        {
            return SelectedObjectLocationOption
                   ?? _objectLocationOptions.FirstOrDefault(option => option.IsLocal)
                   ?? ObjectLocationOption.Local;
        }

        var existing = _objectLocationOptions.FirstOrDefault(option => option.MatchesPathCode(pathCode));
        if (existing != null)
        {
            return existing;
        }

        return ObjectLocationOption.FromPathCode(pathCode);
    }

    private bool TrySelectExistingTab(string tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            return false;
        }

        var existing = Tabs.FirstOrDefault(tab =>
            string.Equals(tab.TabId, tabId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            return false;
        }

        SelectedTab = existing;
        return true;
    }

    private static string BuildSpecsTabId(string objectName, ObjectLocationOption locationOption)
    {
        return $"specs:{NormalizeTabObjectName(objectName)}:{NormalizeLocationTabKey(locationOption)}";
    }

    private static string BuildQueryTabId(string objectName, ObjectLocationOption locationOption, bool isBusinessView)
    {
        string objectKind = isBusinessView ? "BSVW" : "TBLE";
        return $"query:{NormalizeTabObjectName(objectName)}:{NormalizeLocationTabKey(locationOption)}:{objectKind}";
    }

    private static string BuildEventRulesTabId(string objectName, ObjectLocationOption locationOption)
    {
        return $"er:{NormalizeTabObjectName(objectName)}:{NormalizeLocationTabKey(locationOption)}";
    }

    private static string NormalizeTabObjectName(string objectName)
    {
        return string.IsNullOrWhiteSpace(objectName)
            ? string.Empty
            : objectName.Trim().ToUpperInvariant();
    }

    private static string NormalizeLocationTabKey(ObjectLocationOption? locationOption)
    {
        if (locationOption == null || locationOption.IsLocal || string.IsNullOrWhiteSpace(locationOption.PathCode))
        {
            return "LOCAL";
        }

        return locationOption.PathCode.Trim().ToUpperInvariant();
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

            var selectedPathCode = _settingsService.Current.ObjectSearchPathCode;
            var selectedLocation = ResolveLocationOption(selectedPathCode);
            if (!MatchesLocation(SelectedObjectLocationOption, selectedLocation))
            {
                SelectedObjectLocationOption = selectedLocation;
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
            new("Interactive Application (APPL)", JdeObjectType.Application),
            new("Business Function Library (BL)", JdeObjectType.BusinessFunctionLibrary),
            new("Business Function Module (BSFN)", JdeObjectType.BusinessFunction),
            new("Business View (BSVW)", JdeObjectType.BusinessView),
            new("Data Structure (DSTR)", JdeObjectType.DataStructure),
            new("Media Object Data Structure (GT)", JdeObjectType.MediaObjectDataStructure),
            new("Table Definition (TBLE)", JdeObjectType.Table),
            new("Batch Application (UBE)", JdeObjectType.Report)
        };
    }
}
