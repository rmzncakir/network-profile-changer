using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NetworkProfileManager.Views
{
    public partial class AdapterListPanel : UserControl
    {
        public AdapterListPanel() => InitializeComponent();

        private void CopyIp_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            string ip = btn.Tag as string ?? "";
            if (string.IsNullOrEmpty(ip) || ip == "—") return;

            Clipboard.SetText(ip);

            // Briefly show ✓ feedback
            var prev = btn.Content;
            btn.Content = "✓";
            var timer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
            timer.Tick += (s, _) => { btn.Content = prev; timer.Stop(); };
            timer.Start();
        }
    }
}
