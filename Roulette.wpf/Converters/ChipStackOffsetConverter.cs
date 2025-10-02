using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Roulette.Wpf.Converters;

public sealed class ChipStackOffsetConverter : IValueConverter
{
    const double ChipOffset = 6d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && index >= 0)
        {
            double offset = index * ChipOffset;
            return new Thickness(0, offset, 0, 0);
        }

        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}