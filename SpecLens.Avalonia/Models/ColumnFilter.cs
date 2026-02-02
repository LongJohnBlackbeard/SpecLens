using System;
using System.Collections.Generic;
using ReactiveUI;

namespace SpecLens.Avalonia.Models;

public sealed class ColumnFilter : ReactiveObject
{
    private string? _description;
    private string _value = string.Empty;
    private ColumnSortState _sortState = ColumnSortState.None;
    private int _sortIndex;
    private bool _isIndexKey;
    private string _headerText = string.Empty;
    private string _headerTitle = string.Empty;
    private string _headerDescription = string.Empty;
    private bool _showHeaderDescription;

    public ColumnFilter(string name, string? sqlName = null, string? description = null, string? dataItem = null)
    {
        Name = name;
        SqlName = sqlName;
        DataItem = string.IsNullOrWhiteSpace(dataItem) ? ExtractDataItem(name) : dataItem;
        _description = description;
        HeaderText = name;
    }

    public string Name { get; }
    public string? SqlName { get; }
    public string DataItem { get; }

    public string? Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public ColumnSortState SortState
    {
        get => _sortState;
        set
        {
            if (EqualityComparer<ColumnSortState>.Default.Equals(_sortState, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _sortState, value);
            this.RaisePropertyChanged(nameof(SortLabel));
            this.RaisePropertyChanged(nameof(IsSortAscending));
            this.RaisePropertyChanged(nameof(IsSortDescending));
        }
    }

    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (_sortIndex == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _sortIndex, value);
            this.RaisePropertyChanged(nameof(SortLabel));
        }
    }

    public bool IsIndexKey
    {
        get => _isIndexKey;
        set => this.RaiseAndSetIfChanged(ref _isIndexKey, value);
    }

    public string HeaderText
    {
        get => _headerText;
        set => this.RaiseAndSetIfChanged(ref _headerText, value);
    }

    public string HeaderTitle
    {
        get => _headerTitle;
        set => this.RaiseAndSetIfChanged(ref _headerTitle, value);
    }

    public string HeaderDescription
    {
        get => _headerDescription;
        set => this.RaiseAndSetIfChanged(ref _headerDescription, value);
    }

    public bool ShowHeaderDescription
    {
        get => _showHeaderDescription;
        set => this.RaiseAndSetIfChanged(ref _showHeaderDescription, value);
    }

    public string SortLabel => SortState switch
    {
        ColumnSortState.Ascending => $"ASC {SortIndex}",
        ColumnSortState.Descending => $"DESC {SortIndex}",
        _ => string.Empty
    };

    public bool IsSortAscending => SortState == ColumnSortState.Ascending;
    public bool IsSortDescending => SortState == ColumnSortState.Descending;

    public override string ToString() => Name;

    private static string ExtractDataItem(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        int index = name.IndexOf('.', StringComparison.Ordinal);
        if (index > 0 && index < name.Length - 1)
        {
            return name.Substring(index + 1);
        }

        return name;
    }
}

public enum ColumnSortState
{
    None,
    Ascending,
    Descending
}
