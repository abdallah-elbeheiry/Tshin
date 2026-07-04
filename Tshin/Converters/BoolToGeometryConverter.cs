using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tshin.Converters;

/// <summary>
/// Returns one of two <see cref="StreamGeometry"/> resources based on a boolean.
/// Usage: Converter={StaticResource BoolToGeometry}
///   with ConverterParameter="EyeOpenIcon|EyeClosedIcon"
///   (or any two resource keys separated by '|').
/// True  → first key, False → second key.
/// </summary>
public class BoolToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || parameter is not string keys) return null;
        var parts = keys.Split('|');
        if (parts.Length != 2) return null;
        var key = b ? parts[0] : parts[1];
        return Application.Current!.TryGetResource(key, null, out var resource) ? resource as StreamGeometry : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
