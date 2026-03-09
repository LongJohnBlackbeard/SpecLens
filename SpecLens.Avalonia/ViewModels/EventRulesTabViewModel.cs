using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using JdeClient.Core.Models;
using SpecLens.Avalonia.Models;
using SpecLens.Avalonia.Services;
using Serilog;

namespace SpecLens.Avalonia.ViewModels;

public sealed class EventRulesTabViewModel : WorkspaceTabViewModel
{
    private readonly IJdeConnectionService _connectionService;
    private readonly JdeObjectInfo _jdeObject;
    private readonly ObjectLocationOption _locationOption;
    private readonly string? _initialFunctionName;
    private int _loadToken;
    private ObservableCollection<JdeEventRulesNode> _nodes = new();
    private JdeEventRulesNode? _selectedNode;
    private string _documentText = string.Empty;
    private string _sourceCodeText = string.Empty;
    private string _headerCodeText = string.Empty;
    private string _sourceScrollTarget = string.Empty;
    private string _documentTitle = string.Empty;
    private string _sourceDocumentTitle = "Source (.c)";
    private string _headerDocumentTitle = "Header (.h)";
    private string _dataStructureName = string.Empty;
    private string _componentConfiguration = string.Empty;
    private IReadOnlyList<JdeSpecMetadataSection> _selectedMetadataSections = Array.Empty<JdeSpecMetadataSection>();
    private JdeSpecMetadataSection? _selectedMetadataSection;
    private string _statusMessage = "Select an item to view event rules.";
    private bool _isLoading;
    private bool _isCodeView;
    private bool _showRawXml;
    private IReadOnlyList<JdeBusinessFunctionCodeDocument>? _cachedBusinessFunctionDocuments;

    public EventRulesTabViewModel(
        JdeObjectInfo jdeObject,
        IJdeConnectionService connectionService,
        ObjectLocationOption? locationOption = null,
        string? initialFunctionName = null)
        : base(
            $"{jdeObject.ObjectName} - Event Rules",
            $"er:{jdeObject.ObjectName}:{(locationOption?.DisplayName ?? ObjectLocationOption.Local.DisplayName)}")
    {
        _jdeObject = jdeObject ?? throw new ArgumentNullException(nameof(jdeObject));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _locationOption = locationOption ?? ObjectLocationOption.Local;
        _initialFunctionName = initialFunctionName;
        DocumentTitle = _jdeObject.ObjectName;
    }

    public string? PathCode => _locationOption.PathCode;

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
            this.RaisePropertyChanged(nameof(ShowEventRulesDocument));
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

    public string SourceCodeText
    {
        get => _sourceCodeText;
        set
        {
            if (string.Equals(_sourceCodeText, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _sourceCodeText, value);
            this.RaisePropertyChanged(nameof(HasDocument));
        }
    }

    public string HeaderCodeText
    {
        get => _headerCodeText;
        set
        {
            if (string.Equals(_headerCodeText, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _headerCodeText, value);
            this.RaisePropertyChanged(nameof(HasDocument));
        }
    }

    public string SourceScrollTarget
    {
        get => _sourceScrollTarget;
        set => this.RaiseAndSetIfChanged(ref _sourceScrollTarget, value);
    }

    public string DocumentTitle
    {
        get => _documentTitle;
        set => this.RaiseAndSetIfChanged(ref _documentTitle, value);
    }

    public string SourceDocumentTitle
    {
        get => _sourceDocumentTitle;
        set => this.RaiseAndSetIfChanged(ref _sourceDocumentTitle, value);
    }

    public string HeaderDocumentTitle
    {
        get => _headerDocumentTitle;
        set => this.RaiseAndSetIfChanged(ref _headerDocumentTitle, value);
    }

    public string DataStructureName
    {
        get => _dataStructureName;
        set => this.RaiseAndSetIfChanged(ref _dataStructureName, value);
    }

    public string ComponentConfiguration
    {
        get => _componentConfiguration;
        set => this.RaiseAndSetIfChanged(ref _componentConfiguration, value);
    }

    public IReadOnlyList<JdeSpecMetadataSection> SelectedMetadataSections
    {
        get => _selectedMetadataSections;
        private set
        {
            this.RaiseAndSetIfChanged(ref _selectedMetadataSections, value);
            if (_selectedMetadataSection != null &&
                !_selectedMetadataSections.Contains(_selectedMetadataSection))
            {
                SelectedMetadataSection = null;
            }

            this.RaisePropertyChanged(nameof(HasMetadataSections));
        }
    }

    public JdeSpecMetadataSection? SelectedMetadataSection
    {
        get => _selectedMetadataSection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMetadataSection, value);
            this.RaisePropertyChanged(nameof(HasSelectedMetadataSection));
        }
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

    public bool IsCodeView
    {
        get => _isCodeView;
        set
        {
            if (_isCodeView == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isCodeView, value);
            this.RaisePropertyChanged(nameof(IsEventRulesView));
            this.RaisePropertyChanged(nameof(ShowEventRulesDocument));
        }
    }

    public bool IsEventRulesView => !IsCodeView;

    public bool HasMetadataSections => SelectedMetadataSections.Count > 0;

    public bool HasSelectedMetadataSection => SelectedMetadataSection != null;

    public bool ShowEventRulesDocument => !IsCodeView && SelectedNode?.HasEventRules == true;

    public bool ShowRawXml
    {
        get => _showRawXml;
        set
        {
            if (_showRawXml == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _showRawXml, value);
            if (IsCodeView || _selectedNode == null)
            {
                return;
            }

            _ = LoadSelectedNodeAsync(_selectedNode);
        }
    }

    public bool HasDocument =>
        !string.IsNullOrWhiteSpace(DocumentText) ||
        !string.IsNullOrWhiteSpace(SourceCodeText) ||
        !string.IsNullOrWhiteSpace(HeaderCodeText);

    public bool ShouldAutoExpandTree =>
        string.Equals(_jdeObject.ObjectType?.Trim(), "NER", StringComparison.OrdinalIgnoreCase);

    private bool IsNamedEventRuleObject =>
        string.Equals(_jdeObject.ObjectType?.Trim(), "BSFN", StringComparison.OrdinalIgnoreCase) &&
        _jdeObject.ObjectName.StartsWith("N", StringComparison.OrdinalIgnoreCase);

    private bool IsCBusinessFunctionObject =>
        string.Equals(_jdeObject.ObjectType?.Trim(), "BSFN", StringComparison.OrdinalIgnoreCase) &&
        _jdeObject.ObjectName.StartsWith("B", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading event rules tree...";
        _cachedBusinessFunctionDocuments = null;

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
                else if (IsCBusinessFunctionObject && !IsNamedEventRuleObject)
                {
                    var firstFunction = root.Children.FirstOrDefault() ?? root;
                    SelectedNode = firstFunction;
                }
            }
            else if (IsCBusinessFunctionObject && !IsNamedEventRuleObject)
            {
                var firstFunction = root.Children.FirstOrDefault() ?? root;
                SelectedNode = firstFunction;
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
        SourceCodeText = string.Empty;
        HeaderCodeText = string.Empty;
        SourceScrollTarget = string.Empty;
        SourceDocumentTitle = "Source (.c)";
        HeaderDocumentTitle = "Header (.h)";
        DataStructureName = string.Empty;
        ComponentConfiguration = string.Empty;
        SelectedMetadataSections = node?.MetadataSections ?? Array.Empty<JdeSpecMetadataSection>();
        SelectedMetadataSection = null;

        if (node == null)
        {
            IsCodeView = IsCBusinessFunctionObject && !IsNamedEventRuleObject;
            DocumentTitle = _jdeObject.ObjectName;
            StatusMessage = IsCodeView
                ? "Select a function to view C source/header."
                : "Select an item to view event rules.";
            return;
        }

        DocumentTitle = $"{_jdeObject.ObjectName} - {node.Name}";
        DataStructureName = BuildDataStructureLabel(node.DataStructureName);
        ComponentConfiguration = BuildComponentConfiguration(node);

        if (!IsCBusinessFunctionObject && !node.HasEventRules)
        {
            IsCodeView = false;
            IsLoading = false;
            StatusMessage = node.Children.Count > 0
                ? "Select an event to view event rules."
                : "No event rules for the selected item.";
            return;
        }

        IsLoading = true;
        IsCodeView = IsCBusinessFunctionObject && !IsNamedEventRuleObject;
        StatusMessage = IsCodeView
            ? "Loading C business function source..."
            : "Loading event rules...";

        try
        {
            if (IsCodeView)
            {
                string? functionName = ResolveBusinessFunctionFunctionName(node);
                var (location, dataSourceOverride, locationLabel) = ResolveBusinessFunctionLocation();
                _cachedBusinessFunctionDocuments ??= await _connectionService.RunExclusiveAsync(
                    client => client.GetBusinessFunctionCodeAsync(
                        _jdeObject.ObjectName,
                        functionName: null,
                        location,
                        dataSourceOverride));

                if (token != _loadToken)
                {
                    return;
                }

                var selectedDocument = SelectBusinessFunctionDocument(_cachedBusinessFunctionDocuments, functionName);
                if (selectedDocument == null)
                {
                    StatusMessage = "No C source was returned for the selected function.";
                    return;
                }

                string selectedFunctionName = string.IsNullOrWhiteSpace(functionName)
                    ? node.Name
                    : functionName;
                if (string.IsNullOrWhiteSpace(selectedFunctionName))
                {
                    selectedFunctionName = selectedDocument.FunctionName;
                }

                string moduleName = ResolveBusinessFunctionModuleName(selectedDocument, _jdeObject.ObjectName);

                DocumentTitle = $"{_jdeObject.ObjectName} - {selectedFunctionName}";
                SourceDocumentTitle = $"{moduleName}.c";
                HeaderDocumentTitle = $"{moduleName}.h";
                SourceCodeText = selectedDocument.SourceCode ?? string.Empty;
                HeaderCodeText = selectedDocument.HeaderCode ?? string.Empty;
                SourceScrollTarget = selectedFunctionName;
                StatusMessage = string.IsNullOrWhiteSpace(HeaderCodeText)
                    ? "Loaded C source (header not found in payload)."
                    : $"Loaded C source and header from {locationLabel}.";
                return;
            }

            var outputFormat = ShowRawXml
                ? JdeEventRulesOutputFormat.Xml
                : JdeEventRulesOutputFormat.Readable;

            var formatted = await _connectionService.RunExclusiveAsync(client =>
            {
                if (_locationOption.IsLocal)
                {
                    return client.GetFormattedEventRulesAsync(node, outputFormat);
                }

                return client.GetFormattedEventRulesAsync(
                    node,
                    outputFormat,
                    useCentralLocation: true,
                    dataSourceOverride: _locationOption.CentralObjectsDataSourceOverride);
            });

            if (token != _loadToken)
            {
                return;
            }

            DocumentText = formatted.Text;
            StatusMessage = formatted.StatusMessage;
            DataStructureName = string.IsNullOrWhiteSpace(formatted.TemplateName)
                ? BuildDataStructureLabel(node.DataStructureName)
                : BuildDataStructureLabel(formatted.TemplateName);
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

    private static JdeBusinessFunctionCodeDocument? SelectBusinessFunctionDocument(
        IReadOnlyList<JdeBusinessFunctionCodeDocument> documents,
        string? functionName)
    {
        if (documents.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(functionName))
        {
            var exact = documents.FirstOrDefault(doc =>
                string.Equals(doc.FunctionName, functionName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }
        }

        return documents[0];
    }

    private string? ResolveBusinessFunctionFunctionName(JdeEventRulesNode node)
    {
        if (node.NodeType == JdeEventRulesNodeType.Function &&
            !string.IsNullOrWhiteSpace(node.Name))
        {
            return node.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_initialFunctionName))
        {
            return _initialFunctionName.Trim();
        }

        return null;
    }

    private (JdeBusinessFunctionCodeLocation Location, string? DataSourceOverride, string LocationLabel)
        ResolveBusinessFunctionLocation()
    {
        if (_locationOption.IsLocal)
        {
            return (JdeBusinessFunctionCodeLocation.Local, null, _locationOption.DisplayName);
        }

        return (
            JdeBusinessFunctionCodeLocation.Central,
            _locationOption.CentralObjectsDataSourceOverride,
            _locationOption.DisplayName);
    }

    private static string ResolveBusinessFunctionModuleName(JdeBusinessFunctionCodeDocument document, string fallbackObjectName)
    {
        if (string.IsNullOrWhiteSpace(document.SourceFileName))
        {
            return fallbackObjectName;
        }

        string candidate = document.SourceFileName.Trim().Replace('\\', '/');
        int slashIndex = candidate.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex + 1 < candidate.Length)
        {
            candidate = candidate[(slashIndex + 1)..];
        }

        int dotIndex = candidate.LastIndexOf('.');
        if (dotIndex > 0)
        {
            candidate = candidate[..dotIndex];
        }

        return string.IsNullOrWhiteSpace(candidate) ? fallbackObjectName : candidate;
    }

    private static string BuildComponentConfiguration(JdeEventRulesNode node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(node.ComponentConfiguration))
        {
            return node.ComponentConfiguration.Trim();
        }

        return string.Empty;
    }

    private static string BuildDataStructureLabel(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return string.Empty;
        }

        return $"Data Structure: {templateName.Trim()}";
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
                : IsCBusinessFunctionObject && !IsNamedEventRuleObject
                    ? "Select a function to view C source/header."
                    : "Select an item to view event rules.";
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateTree();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(UpdateTree).GetTask();
    }
}
