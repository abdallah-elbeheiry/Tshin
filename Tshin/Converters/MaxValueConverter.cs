using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tshin.Converters;

public class MaxValueConverter : IValueConverter
{
    private const string InfinitySymbol = "\u221E";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (double.IsInfinity(d) || double.IsNaN(d) || d >= (double)decimal.MaxValue)
                return InfinitySymbol;
            return d.ToString("0.##", CultureInfo.InvariantCulture);
        }
        return value?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (s.Trim().Equals(InfinitySymbol) || s.Trim().Equals("infinity", StringComparison.OrdinalIgnoreCase))
                return double.MaxValue;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return 0.0;
        }
        return 0.0;
    }
}
