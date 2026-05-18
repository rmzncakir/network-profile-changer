using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NetworkProfileManager.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool v = value is bool b && b;
            if (Invert) v = !v;
            return v ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility vis && vis == Visibility.Visible;
    }
}
