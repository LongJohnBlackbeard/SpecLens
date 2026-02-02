using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using JdeClient.Core.Models;

namespace SpecLens.Avalonia.Services;

public interface IDataDictionaryInfoService : INotifyPropertyChanged
{
    string DataItem { get; }
    string Alias { get; }
    string ColumnName { get; }
    string Source { get; }
    string Name { get; }
    string Title { get; }
    string ColumnTitle { get; }
    string ColumnTitle2 { get; }
    string Description { get; }
    string RowDescription { get; }
    string Glossary { get; }
    string SystemCode { get; }
    string TypeCode { get; }
    string Length { get; }
    string Decimals { get; }
    string DisplayDecimals { get; }
    string DefaultValue { get; }
    string GlossaryGroup { get; }
    string NextNumberSystem { get; }
    string NextNumberIndex { get; }
    string DisplayRuleBf { get; }
    string EditRuleBf { get; }
    string SearchForm { get; }
    string SecurityFlag { get; }
    string EditRule { get; }
    string EditRuleParm1 { get; }
    string EditRuleParm2 { get; }
    string UpperCaseOnly { get; }
    string AllowBlankEntry { get; }
    string AutoInclude { get; }
    string DoNotTotal { get; }
    bool HasSelection { get; }

    Task ShowForDataItemAsync(string dataItem, string? columnName, string? source, CancellationToken cancellationToken = default);
    void Clear();
}

public sealed partial class DataDictionaryInfoService : ReactiveObject, IDataDictionaryInfoService
{
    private static readonly ConcurrentDictionary<string, JdeDataDictionaryDetails> DetailsCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, JdeDataDictionaryItemName> NameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, JdeDataDictionaryTitle> TitleCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DataDictionaryOverrides> OverridesCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> GlossaryGroupCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly IJdeConnectionService _connectionService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _dataItemKey = string.Empty;

    private string _dataItem = string.Empty;
    private string _alias = string.Empty;
    private string _columnName = string.Empty;
    private string _source = string.Empty;
    private string _name = string.Empty;
    private string _title = string.Empty;
    private string _columnTitle = string.Empty;
    private string _columnTitle2 = string.Empty;
    private string _description = string.Empty;
    private string _rowDescription = string.Empty;
    private string _glossary = string.Empty;
    private string _systemCode = string.Empty;
    private string _typeCode = string.Empty;
    private string _length = string.Empty;
    private string _decimals = string.Empty;
    private string _displayDecimals = string.Empty;
    private string _defaultValue = string.Empty;
    private string _glossaryGroup = string.Empty;
    private string _nextNumberSystem = string.Empty;
    private string _nextNumberIndex = string.Empty;
    private string _displayRuleBf = string.Empty;
    private string _editRuleBf = string.Empty;
    private string _searchForm = string.Empty;
    private string _securityFlag = string.Empty;
    private string _editRule = string.Empty;
    private string _editRuleParm1 = string.Empty;
    private string _editRuleParm2 = string.Empty;
    private string _upperCaseOnly = string.Empty;
    private string _allowBlankEntry = string.Empty;
    private string _autoInclude = string.Empty;
    private string _doNotTotal = string.Empty;
    private bool _hasSelection;

    public string DataItem
    {
        get => _dataItem;
        private set => this.RaiseAndSetIfChanged(ref _dataItem, value);
    }

    public string Alias
    {
        get => _alias;
        private set => this.RaiseAndSetIfChanged(ref _alias, value);
    }

    public string ColumnName
    {
        get => _columnName;
        private set => this.RaiseAndSetIfChanged(ref _columnName, value);
    }

    public string Source
    {
        get => _source;
        private set => this.RaiseAndSetIfChanged(ref _source, value);
    }

    public string Name
    {
        get => _name;
        private set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string ColumnTitle
    {
        get => _columnTitle;
        private set => this.RaiseAndSetIfChanged(ref _columnTitle, value);
    }

    public string ColumnTitle2
    {
        get => _columnTitle2;
        private set => this.RaiseAndSetIfChanged(ref _columnTitle2, value);
    }

    public string Description
    {
        get => _description;
        private set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public string RowDescription
    {
        get => _rowDescription;
        private set => this.RaiseAndSetIfChanged(ref _rowDescription, value);
    }

    public string Glossary
    {
        get => _glossary;
        private set => this.RaiseAndSetIfChanged(ref _glossary, value);
    }

    public string SystemCode
    {
        get => _systemCode;
        private set => this.RaiseAndSetIfChanged(ref _systemCode, value);
    }

    public string TypeCode
    {
        get => _typeCode;
        private set => this.RaiseAndSetIfChanged(ref _typeCode, value);
    }

    public string Length
    {
        get => _length;
        private set => this.RaiseAndSetIfChanged(ref _length, value);
    }

    public string Decimals
    {
        get => _decimals;
        private set => this.RaiseAndSetIfChanged(ref _decimals, value);
    }

    public string DisplayDecimals
    {
        get => _displayDecimals;
        private set => this.RaiseAndSetIfChanged(ref _displayDecimals, value);
    }

    public string DefaultValue
    {
        get => _defaultValue;
        private set => this.RaiseAndSetIfChanged(ref _defaultValue, value);
    }

    public string GlossaryGroup
    {
        get => _glossaryGroup;
        private set => this.RaiseAndSetIfChanged(ref _glossaryGroup, value);
    }

    public string NextNumberSystem
    {
        get => _nextNumberSystem;
        private set => this.RaiseAndSetIfChanged(ref _nextNumberSystem, value);
    }

    public string NextNumberIndex
    {
        get => _nextNumberIndex;
        private set => this.RaiseAndSetIfChanged(ref _nextNumberIndex, value);
    }

    public string DisplayRuleBf
    {
        get => _displayRuleBf;
        private set => this.RaiseAndSetIfChanged(ref _displayRuleBf, value);
    }

    public string EditRuleBf
    {
        get => _editRuleBf;
        private set => this.RaiseAndSetIfChanged(ref _editRuleBf, value);
    }

    public string SearchForm
    {
        get => _searchForm;
        private set => this.RaiseAndSetIfChanged(ref _searchForm, value);
    }

    public string SecurityFlag
    {
        get => _securityFlag;
        private set => this.RaiseAndSetIfChanged(ref _securityFlag, value);
    }

    public string EditRule
    {
        get => _editRule;
        private set => this.RaiseAndSetIfChanged(ref _editRule, value);
    }

    public string EditRuleParm1
    {
        get => _editRuleParm1;
        private set => this.RaiseAndSetIfChanged(ref _editRuleParm1, value);
    }

    public string EditRuleParm2
    {
        get => _editRuleParm2;
        private set => this.RaiseAndSetIfChanged(ref _editRuleParm2, value);
    }

    public string UpperCaseOnly
    {
        get => _upperCaseOnly;
        private set => this.RaiseAndSetIfChanged(ref _upperCaseOnly, value);
    }

    public string AllowBlankEntry
    {
        get => _allowBlankEntry;
        private set => this.RaiseAndSetIfChanged(ref _allowBlankEntry, value);
    }

    public string AutoInclude
    {
        get => _autoInclude;
        private set => this.RaiseAndSetIfChanged(ref _autoInclude, value);
    }

    public string DoNotTotal
    {
        get => _doNotTotal;
        private set => this.RaiseAndSetIfChanged(ref _doNotTotal, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
    }

    public DataDictionaryInfoService(IJdeConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task ShowForDataItemAsync(string dataItem, string? columnName, string? source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataItem))
        {
            Clear();
            return;
        }

        if (string.Equals(_dataItemKey, dataItem, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ColumnName, columnName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Source, source ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(dataItem))
            {
                Clear();
                return;
            }

            _dataItemKey = dataItem;
            Alias = dataItem;
            ColumnName = columnName ?? string.Empty;
            Source = source ?? string.Empty;
            HasSelection = true;

            var details = await GetDetailsAsync(dataItem, cancellationToken);
            var nameItem = await GetNameAsync(dataItem, cancellationToken);
            var titleItem = await GetTitleAsync(dataItem, cancellationToken);
            var overrides = await GetOverridesAsync(dataItem, cancellationToken);
            var glossaryGroupValue = details != null ? FormatChar(details.GlossaryGroup) : string.Empty;
            if (string.IsNullOrWhiteSpace(glossaryGroupValue))
            {
                glossaryGroupValue = await GetGlossaryGroupAsync(dataItem, cancellationToken) ?? string.Empty;
            }

            string title1 = titleItem?.Title1 ?? string.Empty;
            string title2 = titleItem?.Title2 ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title1) && string.IsNullOrWhiteSpace(title2) && details != null)
            {
                title1 = GetDictionaryText(details, 'C');
            }

            string displayDataItem = !string.IsNullOrWhiteSpace(overrides?.DataItemLong)
                ? overrides.DataItemLong
                : details != null && !string.IsNullOrWhiteSpace(details.DictionaryName)
                    ? details.DictionaryName
                    : dataItem;

            DataItem = displayDataItem;
            Name = nameItem?.Name ?? string.Empty;
            Title = titleItem?.CombinedTitle ?? string.Empty;
            ColumnTitle = title1;
            ColumnTitle2 = title2;
            Description = details != null ? GetDictionaryText(details, 'A') : string.Empty;
            RowDescription = details != null ? GetDictionaryText(details, 'R') : string.Empty;
            Glossary = details != null ? GetDictionaryText(details, 'H') : string.Empty;
            SystemCode = details?.SystemCode ?? string.Empty;
            TypeCode = details != null ? FormatTypeCode(details) : string.Empty;
            Length = details?.Length.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            Decimals = details?.Decimals.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            DisplayDecimals = details?.DisplayDecimals.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            DefaultValue = details?.DefaultValue ?? string.Empty;
            GlossaryGroup = glossaryGroupValue;
            NextNumberSystem = details?.NextNumberSystem ?? string.Empty;
            NextNumberIndex = details?.NextNumberIndex.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            DisplayRuleBf = details?.DisplayRuleBfnName ?? string.Empty;
            EditRuleBf = details?.EditRuleBfnName ?? string.Empty;
            SearchForm = details?.SearchFormName ?? string.Empty;
            SecurityFlag = details != null ? FormatChar(details.SecurityFlag) : string.Empty;
            EditRule = details?.As400EditRule ?? string.Empty;
            EditRuleParm1 = details?.As400EditParm1 ?? string.Empty;
            EditRuleParm2 = details?.As400EditParm2 ?? string.Empty;
            UpperCaseOnly = NormalizeFlag(overrides?.UpperCaseOnly);
            AllowBlankEntry = NormalizeFlag(overrides?.AllowBlankEntry);
            AutoInclude = NormalizeFlag(overrides?.AutoInclude);
            DoNotTotal = NormalizeFlag(overrides?.DoNotTotal);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Clear()
    {
        _dataItemKey = string.Empty;
        DataItem = string.Empty;
        Alias = string.Empty;
        ColumnName = string.Empty;
        Source = string.Empty;
        Name = string.Empty;
        Title = string.Empty;
        ColumnTitle = string.Empty;
        ColumnTitle2 = string.Empty;
        Description = string.Empty;
        RowDescription = string.Empty;
        Glossary = string.Empty;
        SystemCode = string.Empty;
        TypeCode = string.Empty;
        Length = string.Empty;
        Decimals = string.Empty;
        DisplayDecimals = string.Empty;
        DefaultValue = string.Empty;
        GlossaryGroup = string.Empty;
        NextNumberSystem = string.Empty;
        NextNumberIndex = string.Empty;
        DisplayRuleBf = string.Empty;
        EditRuleBf = string.Empty;
        SearchForm = string.Empty;
        SecurityFlag = string.Empty;
        EditRule = string.Empty;
        EditRuleParm1 = string.Empty;
        EditRuleParm2 = string.Empty;
        UpperCaseOnly = string.Empty;
        AllowBlankEntry = string.Empty;
        AutoInclude = string.Empty;
        DoNotTotal = string.Empty;
        HasSelection = false;
    }

    private sealed class DataDictionaryOverrides
    {
        public string DataItemLong { get; init; } = string.Empty;
        public string UpperCaseOnly { get; init; } = string.Empty;
        public string AllowBlankEntry { get; init; } = string.Empty;
        public string AutoInclude { get; init; } = string.Empty;
        public string DoNotTotal { get; init; } = string.Empty;
    }

    private static string FormatChar(char value)
    {
        return value == '\0' || char.IsWhiteSpace(value)
            ? string.Empty
            : value.ToString();
    }

    private static string FormatTypeCode(JdeDataDictionaryDetails details)
    {
        char code = details.TypeCode;
        if (code == 'S')
        {
            return string.Equals(details.As400Class, "DATEW", StringComparison.OrdinalIgnoreCase)
                ? "Date"
                : "Math Numeric";
        }

        if (code == 'A' && details.Length == 1)
        {
            return "Char";
        }

        return code switch
        {
            'A' => "String",
            'N' => "Numeric",
            'D' => "Date",
            'B' => "Boolean",
            'T' => "Time",
            _ => FormatChar(code)
        };
    }

    private static string NormalizeFlag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 1)
        {
            char flag = char.ToUpperInvariant(trimmed[0]);
            if (flag == 'Y' || flag == 'N')
            {
                return flag.ToString();
            }
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric == 1 ? "Y" : "N";
        }

        return trimmed;
    }

    private async Task<DataDictionaryOverrides?> GetOverridesAsync(string dataItem, CancellationToken cancellationToken)
    {
        if (OverridesCache.TryGetValue(dataItem, out var cached))
        {
            return cached;
        }

        var filters = new[]
        {
            new JdeFilter
            {
                ColumnName = "DTAI",
                Operator = JdeFilterOperator.Equals,
                Value = dataItem
            }
        };

        var result = await _connectionService.RunExclusiveAsync(
            client => client.QueryTableAsync(
                "F9210",
                filters,
                maxRows: 1,
                cancellationToken: cancellationToken),
            cancellationToken);

        var row = result.Rows.FirstOrDefault();
        if (row == null)
        {
            return null;
        }

        var overrides = new DataDictionaryOverrides
        {
            DataItemLong = ReadRowValue(row, "OWDI"),
            UpperCaseOnly = ReadRowValue(row, "UPER"),
            AllowBlankEntry = ReadRowValue(row, "ALBK"),
            AutoInclude = ReadRowValue(row, "AUIN"),
            DoNotTotal = ReadRowValue(row, "CNTT")
        };

        OverridesCache[dataItem] = overrides;
        return overrides;
    }

    private async Task<string?> GetGlossaryGroupAsync(string dataItem, CancellationToken cancellationToken)
    {
        if (GlossaryGroupCache.TryGetValue(dataItem, out var cached))
        {
            return cached;
        }

        var filters = new[]
        {
            new JdeFilter
            {
                ColumnName = "DTAI",
                Operator = JdeFilterOperator.Equals,
                Value = dataItem
            }
        };

        var result = await _connectionService.RunExclusiveAsync(
            client => client.QueryTableAsync(
                "F9200",
                filters,
                maxRows: 1,
                cancellationToken: cancellationToken),
            cancellationToken);

        var row = result.Rows.FirstOrDefault();
        if (row == null)
        {
            return null;
        }

        string glossaryGroup = ReadRowValue(row, "GG");
        if (!string.IsNullOrWhiteSpace(glossaryGroup))
        {
            GlossaryGroupCache[dataItem] = glossaryGroup;
        }

        return glossaryGroup;
    }

    private static string ReadRowValue(IReadOnlyDictionary<string, object> row, string columnName)
    {
        if (row.TryGetValue(columnName, out var value))
        {
            return value?.ToString()?.Trim() ?? string.Empty;
        }

        foreach (var entry in row)
        {
            if (!entry.Key.Contains(columnName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = entry.Value?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private async Task<JdeDataDictionaryDetails?> GetDetailsAsync(string dataItem, CancellationToken cancellationToken)
    {
        if (DetailsCache.TryGetValue(dataItem, out var cached))
        {
            return cached;
        }

        var results = await _connectionService.RunExclusiveAsync(
            client => client.GetDataDictionaryDetailsAsync(new[] { dataItem }, cancellationToken),
            cancellationToken);

        var details = results.FirstOrDefault(item =>
            string.Equals(item.DataItem, dataItem, StringComparison.OrdinalIgnoreCase));
        if (details != null)
        {
            DetailsCache[dataItem] = details;
        }

        return details;
    }

    private async Task<JdeDataDictionaryItemName?> GetNameAsync(string dataItem, CancellationToken cancellationToken)
    {
        if (NameCache.TryGetValue(dataItem, out var cached))
        {
            return cached;
        }

        var results = await _connectionService.RunExclusiveAsync(
            client => client.GetDataDictionaryItemNamesAsync(new[] { dataItem }, cancellationToken),
            cancellationToken);

        var nameItem = results.FirstOrDefault(item =>
            string.Equals(item.DataItem, dataItem, StringComparison.OrdinalIgnoreCase));
        if (nameItem != null)
        {
            NameCache[dataItem] = nameItem;
        }

        return nameItem;
    }

    private async Task<JdeDataDictionaryTitle?> GetTitleAsync(string dataItem, CancellationToken cancellationToken)
    {
        if (TitleCache.TryGetValue(dataItem, out var cached))
        {
            return cached;
        }

        var results = await _connectionService.RunExclusiveAsync(
            client => client.GetDataDictionaryTitlesAsync(new[] { dataItem }, cancellationToken),
            cancellationToken);

        var titleItem = results.FirstOrDefault(item =>
            string.Equals(item.DataItem, dataItem, StringComparison.OrdinalIgnoreCase));
        if (titleItem != null)
        {
            TitleCache[dataItem] = titleItem;
        }

        return titleItem;
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
}


