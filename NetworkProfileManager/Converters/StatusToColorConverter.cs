using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NetworkProfileManager.Converters
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class StatusToColorConverter : IValueConverter
    {
        // Fallbacks if the theme dictionary isn't loaded (e.g. design time).
        private static readonly SolidColorBrush ConnectedFallback    = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        private static readonly SolidColorBrush DisconnectedFallback = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool connected = value is bool b && b;
            var key = connected ? "SuccessBrush" : "DangerBrush";
            // Resolve against the active theme so the dot follows Light/Dark.
            if (Application.Current?.TryFindResource(key) is Brush themed)
                return themed;
            return connected ? ConnectedFallback : DisconnectedFallback;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
