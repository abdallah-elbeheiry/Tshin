using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tshin.Converters;

/// <summary>
/// Converts a boolean to a display string.
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "True" : "False";
        return "False";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && bool.TryParse(s, out var result))
            return result;
        return false;
    }
}