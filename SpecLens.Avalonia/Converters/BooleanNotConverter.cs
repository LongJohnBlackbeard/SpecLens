using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SpecLens.Avalonia.Converters;

public sealed class BooleanNotConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean ? !boolean : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean ? !boolean : value;
    }
}
