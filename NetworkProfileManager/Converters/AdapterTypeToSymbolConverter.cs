using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace NetworkProfileManager.Converters
{
    /// <summary>
    /// Maps NetworkInterfaceType.ToString() → SymbolRegular icon for adapter cards.
    /// </summary>
    public class AdapterTypeToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Wireless80211"    => SymbolRegular.Wifi124,
                "Bluetooth"        => SymbolRegular.Bluetooth24,
                "Ppp"              => SymbolRegular.Globe24,        // VPN / PPP
                "Tunnel"           => SymbolRegular.ShieldLock20,   // Tailscale, WireGuard
                "GigabitEthernet"  => SymbolRegular.PlugConnected24,
                "FastEthernetFx"   => SymbolRegular.PlugConnected24,
                "FastEthernetT"    => SymbolRegular.PlugConnected24,
                "Ethernet"         => SymbolRegular.PlugConnected24,
                "Ethernet3Megabit" => SymbolRegular.PlugConnected24,
                "Loopback"         => SymbolRegular.ArrowCircleRight20,
                _                  => SymbolRegular.NetworkAdapter16,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
