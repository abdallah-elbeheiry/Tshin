using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tshin.Converters;

/// <summary>
/// Checks if a component type string matches a parameter and returns visible/collapsed.
/// Used to show/hide the correct value editor for a component type.
/// </summary>
public class ComponentTypeVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string type && parameter is string expected)
        {
            return type.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}