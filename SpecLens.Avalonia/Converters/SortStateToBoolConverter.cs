using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using SpecLens.Avalonia.Models;

namespace SpecLens.Avalonia.Converters;

public sealed class SortStateToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ColumnSortState state)
        {
            return false;
        }

        string? mode = parameter as string;
        if (string.IsNullOrWhiteSpace(mode))
        {
            return state != ColumnSortState.None;
        }

        return mode.ToLowerInvariant() switch
        {
            "ascending" => state == ColumnSortState.Ascending,
            "descending" => state == ColumnSortState.Descending,
            "active" => state != ColumnSortState.None,
            _ => state != ColumnSortState.None
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
