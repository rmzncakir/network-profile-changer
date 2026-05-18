using System;
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
        public int    TimeoutMs { get; set; } = 500;
        public int    MaxParallel { get; set; } = 50;
    }

    public class IpScannerViewModel : INotifyPropertyChanged
    {
        private readonly IpScannerService _svc = new IpScannerService();
        private CancellationTokenSource? _cts;

        private static readonly string SettingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scanner_settings.json");

        private string _startIp   = "";
        private string _endIp     = "";
        private int    _timeout   = 500;
        private int    _parallel  = 50;
        private bool   _scanning;
        private int    _progress;
        private int    _total;
        private string _statusText = "Hazır";

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
                _startIp  = s.StartIp;
                _endIp    = s.EndIp;
                _timeout  = s.TimeoutMs;
                _parallel = s.MaxParallel;
            }
            catch { /* ignore corrupt settings */ }
        }

        private void SaveSettings()
        {
            try
            {
                var s = new ScannerSettings
                {
                    StartIp    = _startIp,
                    EndIp      = _endIp,
                    TimeoutMs  = _timeout,
                    MaxParallel = _parallel
                };
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(s, Formatting.Indented));
            }
            catch { /* best-effort */ }
        }

        private async void StartScan()
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
                await _svc.ScanRangeAsync(StartIp, EndIp, TimeoutMs, MaxParallel, prog, _cts.Token);
                StatusText = Loc.Format("Status.ScanComplete", Results.Count);
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

            var sb = new StringBuilder();
            sb.AppendLine("IP,Status,Time(ms),Hostname,MAC");
            foreach (var r in Results)
                sb.AppendLine($"{r.IpAddress},{(r.IsOnline ? "Online" : "Offline")},{r.ResponseTime},{r.HostName},{r.MacAddress}");

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            StatusText = Loc.Format("Status.CsvExported", dlg.FileName);
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
