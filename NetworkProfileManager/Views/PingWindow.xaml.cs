using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Views
{
    public partial class PingWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly string _host;
        private bool _running;

        private static readonly SolidColorBrush BrushSuccess  = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        private static readonly SolidColorBrush BrushTimeout  = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        private static readonly SolidColorBrush BrushTtl      = new SolidColorBrush(Color.FromRgb(0xFF, 0x6D, 0x00));
        private static readonly SolidColorBrush BrushSubtext  = new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE));

        public PingWindow(string host)
        {
            InitializeComponent();
            _host = host;
            TitleHost.Text = host;
            Loaded += async (s, e) => await RunPingAsync();
        }

        private async Task RunPingAsync()
        {
            _running = true;
            RetryBtn.IsEnabled = false;
            ResultStack.Children.Clear();
            SummaryBorder.Visibility = Visibility.Collapsed;
            StatusDot.Fill = BrushSubtext;
            StatusText.Text = Loc.Format("Ping.Pinging", _host);

            var times = new List<long>();

            using (var ping = new Ping())
            {
                for (int i = 1; i <= 4; i++)
                {
                    if (!_running) break;
                    try
                    {
                        var reply = await ping.SendPingAsync(_host, 2000);
                        Dispatcher.Invoke(() => AddResult(reply, i, times));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => AddError(ex.Message, i));
                    }

                    if (i < 4 && _running)
                        await Task.Delay(1000);
                }
            }

            Dispatcher.Invoke(() => ShowSummary(times));
            _running = false;
            RetryBtn.IsEnabled = true;
        }

        private void AddResult(PingReply reply, int seq, List<long> times)
        {
            SolidColorBrush color;
            string text;

            switch (reply.Status)
            {
                case IPStatus.Success:
                    color = BrushSuccess;
                    text  = $"[{seq}]  {reply.Address}  —  {reply.RoundtripTime} ms  TTL={reply.Options?.Ttl}";
                    times.Add(reply.RoundtripTime);
                    StatusDot.Fill = BrushSuccess;
                    StatusText.Text = Loc.Format("Ping.ReplyReceived", reply.RoundtripTime);
                    break;
                case IPStatus.TtlExpired:
                    color = BrushTtl;
                    text  = $"[{seq}]  {Loc.Get("Ping.TtlExpired", "TTL süresi doldu")}";
                    StatusDot.Fill = BrushTtl;
                    StatusText.Text = Loc.Get("Ping.TtlExpired", "TTL süresi doldu");
                    break;
                default:
                    color = BrushTimeout;
                    text  = $"[{seq}]  {Loc.Get("Ping.Timeout", "Zaman aşımı")} ({reply.Status})";
                    StatusDot.Fill = BrushTimeout;
                    StatusText.Text = Loc.Get("Ping.Timeout", "Zaman aşımı");
                    break;
            }

            var tb = new TextBlock
            {
                Text       = text,
                Foreground = color,
                FontSize   = 12,
                FontFamily = new FontFamily("Consolas"),
                Margin     = new Thickness(0, 3, 0, 3)
            };
            ResultStack.Children.Add(tb);
            ResultScroll.ScrollToBottom();
        }

        private void AddError(string message, int seq)
        {
            var tb = new TextBlock
            {
                Text       = $"[{seq}]  {Loc.Get("Ping.Error", "Hata")}: {message}",
                Foreground = BrushTimeout,
                FontSize   = 12,
                FontFamily = new FontFamily("Consolas"),
                Margin     = new Thickness(0, 3, 0, 3)
            };
            ResultStack.Children.Add(tb);
            StatusDot.Fill = BrushTimeout;
            StatusText.Text = Loc.Get("Ping.Error", "Hata");
        }

        private void ShowSummary(List<long> times)
        {
            StatusText.Text = Loc.Get("Ping.Completed", "Tamamlandı");
            SummaryBorder.Visibility = Visibility.Visible;

            if (times.Count == 0)
            {
                SummaryText.Text = Loc.Get("Ping.NoReply", "Yanıt alınamadı.");
                return;
            }

            long min = long.MaxValue, max = 0, sum = 0;
            foreach (var t in times)
            {
                if (t < min) min = t;
                if (t > max) max = t;
                sum += t;
            }
            long avg = sum / times.Count;
            SummaryText.Text = Loc.Format("Ping.Summary", min, max, avg, times.Count);
        }

        private async void RetryClick(object sender, RoutedEventArgs e)
            => await RunPingAsync();

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            _running = false;
            Close();
        }
    }
}
