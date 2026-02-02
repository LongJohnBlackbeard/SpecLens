using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using JdeClient.Core.Models;
using SpecLens.Avalonia.Services;
using Serilog;

namespace SpecLens.Avalonia.ViewModels;

public sealed class EventRulesTabViewModel : WorkspaceTabViewModel
{
    private readonly IJdeConnectionService _connectionService;
    private readonly JdeObjectInfo _jdeObject;
    private readonly string? _initialFunctionName;
    private int _loadToken;
    private ObservableCollection<JdeEventRulesNode> _nodes = new();
    private JdeEventRulesNode? _selectedNode;
    private string _documentText = string.Empty;
    private string _documentTitle = string.Empty;
    private string _dataStructureName = string.Empty;
    private string _statusMessage = "Select an item to view event rules.";
    private bool _isLoading;

    public EventRulesTabViewModel(
        JdeObjectInfo jdeObject,
        IJdeConnectionService connectionService,
        string? initialFunctionName = null)
        : base($"{jdeObject.ObjectName} - Event Rules", $"er:{jdeObject.ObjectName}")
    {
        _jdeObject = jdeObject ?? throw new ArgumentNullException(nameof(jdeObject));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _initialFunctionName = initialFunctionName;
        DocumentTitle = _jdeObject.ObjectName;
    }

    public ObservableCollection<JdeEventRulesNode> Nodes
    {
        get => _nodes;
        private set => this.RaiseAndSetIfChanged(ref _nodes, value);
    }

    public JdeEventRulesNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (EqualityComparer<JdeEventRulesNode?>.Default.Equals(_selectedNode, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            _ = LoadSelectedNodeAsync(value);
        }
    }

    public string DocumentText
    {
        get => _documentText;
        set
        {
            if (string.Equals(_documentText, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _documentText, value);
            this.RaisePropertyChanged(nameof(HasDocument));
        }
    }

    public string DocumentTitle
    {
        get => _documentTitle;
        set => this.RaiseAndSetIfChanged(ref _documentTitle, value);
    }

    public string DataStructureName
    {
        get => _dataStructureName;
        set => this.RaiseAndSetIfChanged(ref _dataStructureName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool HasDocument => !string.IsNullOrWhiteSpace(DocumentText);

    public bool ShouldAutoExpandTree =>
        string.Equals(_jdeObject.ObjectType?.Trim(), "NER", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading event rules tree...";

        try
        {
            var root = await _connectionService.RunExclusiveAsync(
                client => client.GetEventRulesTreeAsync(_jdeObject));

            if (ShouldAutoExpandTree)
            {
                root.IsExpanded = true;
            }

            await UpdateTreeAsync(root);

            if (!string.IsNullOrWhiteSpace(_initialFunctionName))
            {
                var target = FindNodeByName(root, _initialFunctionName);
                if (target != null)
                {
                    SelectedNode = target;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Log.Error(ex, "Failed to load event rules tree for {Object}", _jdeObject.ObjectName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSelectedNodeAsync(JdeEventRulesNode? node)
    {
        int token = ++_loadToken;
        DocumentText = string.Empty;
        DataStructureName = string.Empty;

        if (node == null)
        {
            DocumentTitle = _jdeObject.ObjectName;
            StatusMessage = "Select an item to view event rules.";
            return;
        }

        DocumentTitle = $"{_jdeObject.ObjectName} - {node.Name}";
        DataStructureName = string.Empty;

        IsLoading = true;
        StatusMessage = "Loading event rules...";

        try
        {
            var formatted = await _connectionService.RunExclusiveAsync(
                client => client.GetFormattedEventRulesAsync(node));

            if (token != _loadToken)
            {
                return;
            }

            DocumentText = formatted.Text;
            StatusMessage = formatted.StatusMessage;
            DataStructureName = string.IsNullOrWhiteSpace(formatted.TemplateName)
                ? string.Empty
                : $"Data Structure: {formatted.TemplateName}";
        }
        catch (Exception ex)
        {
            if (token != _loadToken)
            {
                return;
            }

            StatusMessage = ex.Message;
            Log.Error(ex, "Failed to load event rules for {Object}", _jdeObject.ObjectName);
        }
        finally
        {
            if (token == _loadToken)
            {
                IsLoading = false;
            }
        }
    }

    private static JdeEventRulesNode? FindNodeByName(JdeEventRulesNode root, string name)
    {
        if (string.Equals(root.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var match = FindNodeByName(child, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private Task UpdateTreeAsync(JdeEventRulesNode root)
    {
        void UpdateTree()
        {
            var items = root.Children.Count == 0
                ? new[] { root }
                : root.Children;
            Nodes = new ObservableCollection<JdeEventRulesNode>(items);
            StatusMessage = root.Children.Count == 0
                ? "No child functions found."
                : "Select a function to view event rules.";
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateTree();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(UpdateTree).GetTask();
    }
}
