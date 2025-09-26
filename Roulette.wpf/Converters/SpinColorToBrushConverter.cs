using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Roulette.Wpf.Converters;

public class SpinColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Red" => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            "Black" => new SolidColorBrush(Color.FromRgb(52, 58, 64)),
            "Green" => new SolidColorBrush(Color.FromRgb(25, 135, 84)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
