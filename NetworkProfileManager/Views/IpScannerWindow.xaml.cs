using System;
using System.Windows;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Views
{
    public partial class IpScannerWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly IpScannerViewModel _vm;

        public IpScannerWindow(string startIp, string endIp, Action<string> applyIpCallback)
        {
            InitializeComponent();

            // ViewModel constructor already calls LoadSettings(); only fall back
            // to the adapter-derived IPs when no saved values exist yet.
            _vm = new IpScannerViewModel();
            if (string.IsNullOrEmpty(_vm.StartIp))
                _vm.StartIp = startIp;
            if (string.IsNullOrEmpty(_vm.EndIp))
                _vm.EndIp = endIp;

            _vm.ApplyIpCallback = ip =>
            {
                applyIpCallback?.Invoke(ip);
                _vm.StatusText = $"IP uygulandı: {ip}";
            };
            DataContext = _vm;
        }

        private void CloseClick(object sender, RoutedEventArgs e) => Close();

        // Copy a single IP address from the inline button inside the DataGrid row
        private void CopyIp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.Tag is string ip && !string.IsNullOrEmpty(ip))
            {
                Clipboard.SetText(ip);
                _vm.StatusText = $"Kopyalandı: {ip}";
            }
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedResult == null) return;
            var r = _vm.SelectedResult;
            string text = $"{r.IpAddress}\t{(r.IsOnline ? "Online" : "Offline")}\t{r.ResponseTime}ms\t{r.HostName}\t{r.MacAddress}";
            Clipboard.SetText(text);
            _vm.StatusText = $"Kopyalandı: {r.IpAddress}";
        }
    }
}
