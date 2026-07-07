using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tshin.Converters;

public class NumericSafeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (double.IsInfinity(d) || double.IsNaN(d) || d >= (double)decimal.MaxValue)
                return (double)decimal.MaxValue;
            return d;
        }

        if (value is int i)
        {
            if (i >= (double)decimal.MaxValue)
                return (double)decimal.MaxValue;
            return (double)i;
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return d;
        if (value is decimal m)
            return (double)m;
        return value;
    }
}
