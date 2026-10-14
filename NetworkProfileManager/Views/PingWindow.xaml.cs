using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shapes;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Views
{
    public partial class PingWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly string _host;
        private bool _running;

        // Fluent theme keys, bound by reference so a theme switch recolours
        // lines that are already on screen.
        private const string Success = "SystemFillColorSuccessBrush";
        private const string Failure = "SystemFillColorCriticalBrush";
        private const string Caution = "SystemFillColorCautionBrush";
        private const string Neutral = "TextFillColorTertiaryBrush";

        public PingWindow(string host)
        {
            InitializeComponent();
            _host = host;
            TitleHost.Text = $" · {host}";
            Loaded += async (s, e) => await RunPingAsync();
        }

        private async Task RunPingAsync()
        {
            _running = true;
            RetryBtn.IsEnabled = false;
            ResultStack.Children.Clear();
            SummaryText.Visibility = Visibility.Collapsed;
            SetStatusColor(Neutral);
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
            switch (reply.Status)
            {
                case IPStatus.Success:
                    times.Add(reply.RoundtripTime);
                    // Success is the normal case: neutral text, colour only on the dot.
                    AddLine(seq, $"{reply.Address}    {reply.RoundtripTime} ms    TTL {reply.Options?.Ttl}", null);
                    SetStatusColor(Success);
                    StatusText.Text = Loc.Format("Ping.ReplyReceived", reply.RoundtripTime);
                    break;
                case IPStatus.TtlExpired:
                    AddLine(seq, Loc.Get("Ping.TtlExpired", "TTL süresi doldu"), Caution);
                    SetStatusColor(Caution);
                    StatusText.Text = Loc.Get("Ping.TtlExpired", "TTL süresi doldu");
                    break;
                default:
                    // "Timed out" says it all; other failures (unreachable, …) name their status.
                    string failure = reply.Status == IPStatus.TimedOut
                        ? Loc.Get("Ping.Timeout", "Zaman aşımı")
                        : $"{Loc.Get("Ping.Error", "Hata")}: {reply.Status}";
                    AddLine(seq, failure, Failure);
                    SetStatusColor(Failure);
                    StatusText.Text = failure;
                    break;
            }
        }

        private void AddError(string message, int seq)
        {
            AddLine(seq, $"{Loc.Get("Ping.Error", "Hata")}: {message}", Failure);
            SetStatusColor(Failure);
            StatusText.Text = Loc.Get("Ping.Error", "Hata");
        }

        private void AddLine(int seq, string text, string? colorKey)
        {
            var number = new Run($"{seq}    ");
            number.SetResourceReference(TextElement.ForegroundProperty, Neutral);

            var body = new Run(text);
            if (colorKey != null)
                body.SetResourceReference(TextElement.ForegroundProperty, colorKey);

            var tb = new TextBlock { Margin = new Thickness(0, 3, 0, 3), TextWrapping = TextWrapping.Wrap };
            tb.Inlines.Add(number);
            tb.Inlines.Add(body);
            ResultStack.Children.Add(tb);
            ResultScroll.ScrollToBottom();
        }

        private void SetStatusColor(string key) => StatusDot.SetResourceReference(Shape.FillProperty, key);

        private void ShowSummary(List<long> times)
        {
            StatusText.Text = Loc.Get("Ping.Completed", "Tamamlandı");
            SummaryText.Visibility = Visibility.Visible;

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
