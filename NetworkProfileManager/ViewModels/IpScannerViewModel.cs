using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NetworkProfileManager.Helpers;
using System.Windows.Input;
using NetworkProfileManager.Commands;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;
using Newtonsoft.Json;

namespace NetworkProfileManager.ViewModels
{
    // Persisted scanner settings
    internal class ScannerSettings
    {
        public string StartIp   { get; set; } = "";
        public string EndIp     { get; set; } = "";
        public int    TimeoutMs { get; set; } = 1000;
        public int    MaxParallel { get; set; } = 128;
        public bool   UseArp      { get; set; } = true;
        public bool   UseTcpProbe { get; set; } = true;

        /// <summary>Interval only — monitoring itself is deliberately not persisted, so
        /// reopening the window never silently resumes background scanning.</summary>
        public int    MonitorIntervalMinutes { get; set; } = 5;
    }

    public class IpScannerViewModel : INotifyPropertyChanged
    {
        private readonly IpScannerService _svc = new IpScannerService();
        private CancellationTokenSource? _cts;

        // Stored alongside every other app data file so it persists even under an
        // ACL-protected install (%ProgramFiles%), where exe-relative writes fail.
        private static string SettingsPath =>
            Path.Combine(AppPaths.DataDirectory, "scanner_settings.json");

        private string _startIp   = "";
        private string _endIp     = "";
        private int    _timeout   = 1000;
        private int    _parallel  = 128;
        private bool   _useArp      = true;
        private bool   _useTcpProbe = true;
        private bool   _scanning;
        private bool   _isMonitoring;
        private int    _monitorMinutes = 5;
        private bool   _baselineTaken;
        private CancellationTokenSource? _monitorCts;
        private readonly HashSet<string> _knownDevices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int    _progress;
        private int    _total;
        private string _statusText = Loc.Get("Common.Ready", "Hazır");

        public string StartIp
        {
            get => _startIp;
            set { _startIp = value; OnPropertyChanged(); SaveSettings(); }
        }
        public string EndIp
        {
            get => _endIp;
            set { _endIp = value; OnPropertyChanged(); SaveSettings(); }
        }
        public int TimeoutMs
        {
            get => _timeout;
            set { _timeout = value; OnPropertyChanged(); SaveSettings(); }
        }
        public int MaxParallel
        {
            get => _parallel;
            set { _parallel = value; OnPropertyChanged(); SaveSettings(); }
        }
        /// <summary>ARP sweep — the main reason firewalled hosts appear at all.</summary>
        public bool UseArp
        {
            get => _useArp;
            set { _useArp = value; OnPropertyChanged(); SaveSettings(); }
        }
        /// <summary>TCP port probe — finds off-subnet hosts and identifies device type.</summary>
        public bool UseTcpProbe
        {
            get => _useTcpProbe;
            set { _useTcpProbe = value; OnPropertyChanged(); SaveSettings(); }
        }

        /// <summary>Re-sweeps on an interval and notifies when a host appears that wasn't there before.</summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                if (_isMonitoring == value) return;
                _isMonitoring = value;
                OnPropertyChanged();
                SaveSettings();
                if (value) StartMonitoring(); else StopMonitoring();
            }
        }

        public int MonitorIntervalMinutes
        {
            get => _monitorMinutes;
            set { _monitorMinutes = value; OnPropertyChanged(); SaveSettings(); }
        }
        public bool IsScanning
        {
            get => _scanning;
            set { _scanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotScanning)); }
        }
        public bool IsNotScanning => !_scanning;
        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressPercent)); }
        }
        public int Total
        {
            get => _total;
            set { _total = value; OnPropertyChanged(); }
        }
        public double ProgressPercent => Total == 0 ? 0 : (double)Progress / Total * 100;
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ScanResult> Results { get; } = new ObservableCollection<ScanResult>();

        public ScanResult? SelectedResult { get; set; }

        public ICommand StartCommand  { get; }
        public ICommand StopCommand   { get; }
        public ICommand ExportCommand { get; }
        public ICommand ApplyIpCommand { get; }

        public Action<string>? ApplyIpCallback { get; set; }

        public IpScannerViewModel()
        {
            LoadSettings();

            StartCommand  = new RelayCommand(_ => StartScan(),  _ => !IsScanning);
            StopCommand   = new RelayCommand(_ => StopScan(),   _ => IsScanning);
            ExportCommand = new RelayCommand(_ => ExportCsv(),  _ => Results.Count > 0 && !IsScanning);
            ApplyIpCommand = new RelayCommand(
                p => { if (p is ScanResult r) ApplyIpCallback?.Invoke(r.IpAddress); },
                p => p is ScanResult r2 && r2.IsOnline && ApplyIpCallback != null);
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var json = File.ReadAllText(SettingsPath);
                var s = JsonConvert.DeserializeObject<ScannerSettings>(json);
                if (s == null) return;
                _startIp     = s.StartIp;
                _endIp       = s.EndIp;
                _timeout     = s.TimeoutMs;
                _parallel    = s.MaxParallel;
                _useArp      = s.UseArp;
                _useTcpProbe = s.UseTcpProbe;
                _monitorMinutes = Math.Max(1, s.MonitorIntervalMinutes);
            }
            catch { /* ignore corrupt settings */ }
        }

        private void SaveSettings()
        {
            try
            {
                var s = new ScannerSettings
                {
                    StartIp     = _startIp,
                    EndIp       = _endIp,
                    TimeoutMs   = _timeout,
                    MaxParallel = _parallel,
                    UseArp      = _useArp,
                    UseTcpProbe = _useTcpProbe,
                    MonitorIntervalMinutes = _monitorMinutes
                };
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(s, Formatting.Indented));
            }
            catch { /* best-effort */ }
        }

        private async void StartScan() => await RunScanAsync(announceNew: false);

        /// <summary>
        /// Runs one sweep. When <paramref name="announceNew"/> is set (monitoring mode)
        /// any host absent from the previous sweep raises a notification instead of
        /// silently appearing in the grid.
        /// </summary>
        private async Task RunScanAsync(bool announceNew)
        {
            if (!ValidateInputs()) return;

            Results.Clear();
            Progress = 0;
            IsScanning = true;

            _cts = new CancellationTokenSource();
            var prog = new Progress<ScanProgress>(p =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Progress = p.Current;
                    Total    = p.Total;
                    if (p.Result != null && p.Result.IsOnline)
                        Results.Add(p.Result);
                    int online = Results.Count;
                    StatusText = Loc.Format("Status.ScanProgress", p.Current, p.Total, online);
                });
            });

            try
            {
                var options = new ScanOptions
                {
                    TimeoutMs   = TimeoutMs,
                    MaxParallel = MaxParallel,
                    UseArp      = UseArp,
                    UseTcpProbe = UseTcpProbe
                };
                await _svc.ScanRangeAsync(StartIp, EndIp, options, prog, _cts.Token);
                StatusText = Loc.Format("Status.ScanComplete", Results.Count);
                ReconcileKnownDevices(announceNew);
            }
            catch (OperationCanceledException)
            {
                StatusText = Loc.Get("Status.ScanCancelled", "Tarama durduruldu.");
            }
            catch (Exception ex)
            {
                NotificationService.Error(ex.Message, Loc.Get("Notify.ScanError", "Tarama Hatası"));
                StatusText = Loc.Get("Status.ScanError", "Hata oluştu.");
            }
            finally
            {
                IsScanning = false;
                _cts = null;
            }
        }

        private void StopScan()
        {
            _cts?.Cancel();
            StatusText = Loc.Get("Status.ScanStopping", "Durduruluyor…");
        }

        // ── Monitoring ─────────────────────────────────────────────────────────

        /// <summary>
        /// Compares the sweep just finished against the previous one. Devices are keyed
        /// by MAC where known, because DHCP can hand the same device a different address
        /// — keying on IP alone would report a lease change as a new device.
        /// </summary>
        private void ReconcileKnownDevices(bool announceNew)
        {
            var current = Results.Where(r => r.IsOnline).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in current)
            {
                string key = string.IsNullOrEmpty(r.MacAddress) ? r.IpAddress : r.MacAddress;
                seen.Add(key);

                if (announceNew && _baselineTaken && !_knownDevices.Contains(key))
                {
                    var detail = string.IsNullOrEmpty(r.DeviceType) ? r.MacAddress : r.DeviceType;
                    NotificationService.Info(
                        string.IsNullOrEmpty(detail) ? r.IpAddress : $"{r.IpAddress} — {detail}",
                        Loc.Get("Monitor.NewDeviceTitle", "Yeni cihaz bulundu"));
                }
            }

            _knownDevices.Clear();
            foreach (var k in seen) _knownDevices.Add(k);
            _baselineTaken = true;
        }

        private void StartMonitoring()
        {
            StopMonitoring();
            _monitorCts = new CancellationTokenSource();
            _ = MonitorLoopAsync(_monitorCts.Token);
            StatusText = Loc.Format("Monitor.Started", MonitorIntervalMinutes);
        }

        private void StopMonitoring()
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            _monitorCts = null;
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, MonitorIntervalMinutes)), ct)
                              .ConfigureAwait(true);
                }
                catch (OperationCanceledException) { return; }

                // Never collide with a sweep the user kicked off by hand.
                if (ct.IsCancellationRequested || IsScanning) continue;

                await RunScanAsync(announceNew: true);
            }
        }

        /// <summary>Called by the view when the window closes, so the loop cannot outlive it.</summary>
        public void Shutdown()
        {
            StopMonitoring();
            _cts?.Cancel();
        }

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = Loc.Get("Csv.SaveTitle", "CSV Olarak Kaydet"),
                FileName   = $"scan_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                DefaultExt = ".csv",
                Filter     = Loc.Get("Csv.Filter", "CSV dosyası|*.csv")
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("IP,Status,FoundBy,Time(ms),Hostname,MAC,DeviceType,OpenPorts");
                foreach (var r in Results)
                    sb.AppendLine(string.Join(",",
                        Csv(r.IpAddress),
                        r.IsOnline ? "Online" : "Offline",
                        Csv(r.FoundByLabel),
                        Csv(r.ResponseTimeLabel),
                        Csv(r.HostName),
                        Csv(r.MacAddress),
                        Csv(r.DeviceType),
                        Csv(r.OpenPorts)));

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                StatusText = Loc.Format("Status.CsvExported", dlg.FileName);
            }
            catch (Exception ex)
            {
                NotificationService.Error(ex.Message, Loc.Get("Csv.ExportError", "Dışa aktarma başarısız"));
                StatusText = Loc.Get("Csv.ExportError", "Dışa aktarma başarısız");
            }
        }

        /// <summary>
        /// Quotes a CSV field and neutralizes spreadsheet formula injection:
        /// hostnames come from attacker-influenceable reverse-DNS, so a value like
        /// "=cmd|'/c calc'!A1" must not execute when the CSV is opened in Excel.
        /// </summary>
        private static string Csv(string? value)
        {
            var s = value ?? "";
            if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@'
                                 || s[0] == '\t' || s[0] == '\r'))
                s = "'" + s;
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private bool ValidateInputs()
        {
            if (!System.Net.IPAddress.TryParse(StartIp, out _) ||
                !System.Net.IPAddress.TryParse(EndIp,   out _))
            {
                NotificationService.Warning(
                    Loc.Get("Validation.IpRangeInvalid", "Geçerli bir IP aralığı girin."),
                    Loc.Get("Notify.InvalidInput", "Geçersiz Giriş"));
                return false;
            }
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
