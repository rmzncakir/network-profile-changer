using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NetworkProfileManager.Converters
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class StatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Connected    = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush Disconnected = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Connected : Disconnected;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
