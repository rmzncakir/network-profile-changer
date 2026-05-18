using System;
using System.Globalization;
using System.Windows.Data;

namespace NetworkProfileManager.Converters
{
    /// <summary>
    /// Parameter format: "TrueValue|FalseValue"
    /// Example: "{Binding IsDhcp, Converter={..}, ConverterParameter='DHCP|Statik'}"
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parts  = (parameter as string ?? "Evet|Hayır").Split('|');
            bool bVal  = value is bool b && b;
            return bVal ? parts[0] : (parts.Length > 1 ? parts[1] : "");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
