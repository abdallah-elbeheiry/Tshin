using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tshin.Converters;

/// <summary>
/// Converts component type strings to short icon-like labels for display in badges.
/// </summary>
public class ComponentTypeToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string type)
        {
            return type switch
            {
                "number" => "#",
                "text" => "T",
                "condition" => "?",
                _ => "?"
            };
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}