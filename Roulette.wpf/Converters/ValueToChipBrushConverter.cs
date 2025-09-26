using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Roulette.Wpf.Converters
{
    public sealed class ValueToChipBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int v)
            {
                return v switch
                {
                    1 => new SolidColorBrush(Colors.White),
                    2 => new SolidColorBrush(Color.FromRgb(25, 118, 210)), // blue
                    5 => new SolidColorBrush(Color.FromRgb(193, 18, 31)),  // red
                    10 => new SolidColorBrush(Color.FromRgb(30, 30, 30)),   // black
                    20 => new SolidColorBrush(Color.FromRgb(25, 135, 84)),  // green
                    50 => new SolidColorBrush(Color.FromRgb(111, 66, 193)), // purple
                    _ => new SolidColorBrush(Color.FromRgb(210, 180, 140)) // tan
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
