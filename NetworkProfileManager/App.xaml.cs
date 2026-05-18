using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;
using NetworkProfileManager.ViewModels;
using NetworkProfileManager.Views;
using Newtonsoft.Json;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;

namespace NetworkProfileManager
{
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private TrayPopupWindow? _trayPopup;
        private DateTime _popupLastClosed = DateTime.MinValue;

        internal bool IsExiting { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Migrate legacy data (exe-relative) into LocalAppData on first run.
            // Must run BEFORE LanguageManager.Initialize so the language preference
            // saved by an older version is picked up correctly.
            NetworkProfileManager.Helpers.AppPaths.MigrateLegacyDataIfPresent();

            // Localization must initialize BEFORE any window is constructed so
            // DynamicResource lookups resolve to the right culture.
            LanguageManager.Initialize();

            // Startup kaydı yoksa otomatik ekle
            if (!StartupHelper.IsStartupEnabled())
                StartupHelper.SetStartup(true);

            bool startMinimized = Array.Exists(e.Args, a => a == "--minimized");

            var savedTheme = LoadSavedTheme();
            ApplyTheme(savedTheme);

            InitTrayIcon();

            NotificationService.TrayFallback = (title, msg) =>
            {
                if (_trayIcon == null) return;
                _trayIcon.Visible = false;
                _trayIcon.Visible = true;
                _trayIcon.BalloonTipIcon = ToolTipIcon.None;
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = msg;
                _trayIcon.ShowBalloonTip(3500);
            };

            _mainWindow = new MainWindow();
            _mainWindow.Closing += (s, args) =>
            {
                if (!IsExiting)
                {
                    args.Cancel = true;
                    _mainWindow!.Hide();
                    return;
                }
                if (_trayIcon != null) _trayIcon.Visible = false;
            };

            if (!startMinimized)
                _mainWindow.Show();
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            IsExiting = true;
            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        // ── Theme persistence ─────────────────────────────────────────
        private static string AppSettingsPath =>
            Path.Combine(NetworkProfileManager.Helpers.AppPaths.DataDirectory, "app_settings.json");

        private static string LoadSavedTheme()
        {
            try
            {
                if (!File.Exists(AppSettingsPath)) return "Dark";
                var json = File.ReadAllText(AppSettingsPath);
                var obj = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(json);
                if (obj != null && obj.TryGetValue("Theme", out var t) && !string.IsNullOrEmpty(t))
                    return t;
            }
            catch { }
            return "Dark";
        }

        private static void SaveTheme(string themeName)
        {
            try
            {
                // Merge into existing settings file so we don't clobber Language,
                // which LanguageManager writes to the same JSON.
                System.Collections.Generic.Dictionary<string, string> obj;
                if (File.Exists(AppSettingsPath))
                {
                    var existing = File.ReadAllText(AppSettingsPath);
                    obj = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(existing)
                          ?? new System.Collections.Generic.Dictionary<string, string>();
                }
                else
                {
                    obj = new System.Collections.Generic.Dictionary<string, string>();
                }
                obj["Theme"] = themeName;
                File.WriteAllText(AppSettingsPath, JsonConvert.SerializeObject(obj, Formatting.Indented));
            }
            catch { }
        }

        // ── Theme ─────────────────────────────────────────────────────
        public static string CurrentThemeName { get; private set; } = "Dark";

        public static void ApplyTheme(string themeName)
        {
            var appTheme = themeName == "Light"
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark;

            ApplicationThemeManager.Apply(appTheme);

            var newDict = new System.Windows.ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{themeName}.xaml",
                                 UriKind.Absolute)
            };

            var dicts = Current.Resources.MergedDictionaries;
            var existing = dicts.FirstOrDefault(d => d.Contains("BackgroundBrush"));
            if (existing != null)
            {
                int idx = dicts.IndexOf(existing);
                dicts.Remove(existing);
                dicts.Insert(idx, newDict);
            }
            else
            {
                dicts.Add(newDict);
            }

            CurrentThemeName = themeName;
            SaveTheme(themeName);
        }

        // ── Tray ──────────────────────────────────────────────────────
        private void InitTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = GetIcon(),
                Text = "Network Profile Manager",
                Visible = true
            };

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ToggleTrayPopup();
            };
            _trayIcon.DoubleClick += (s, e) =>
            {
                _trayPopup?.Close();
                ShowMainWindow();
            };
        }

        // ── Tray popup ────────────────────────────────────────────────
        private void ToggleTrayPopup()
        {
            if (_trayPopup != null)
            {
                _trayPopup.Close();
                return;
            }

            if ((DateTime.Now - _popupLastClosed).TotalMilliseconds < 400)
                return;

            ShowTrayPopup();
        }

        private void ShowTrayPopup()
        {
            _trayPopup = new TrayPopupWindow();
            _trayPopup.OnBeforeClose = () =>
            {
                _popupLastClosed = DateTime.Now;
                _trayPopup = null;
            };

            var vm = _mainWindow?.DataContext as MainViewModel;
            _trayPopup.Rebuild(
                vm,
                openMain: () => Dispatcher.Invoke(ShowMainWindow),
                openSettings: () => Dispatcher.Invoke(ShowThemeSettings),
                exit: () => Dispatcher.Invoke(() =>
                {
                    IsExiting = true;
                    _trayIcon!.Visible = false;
                    Shutdown();
                }));

            _trayPopup.Show();
            _trayPopup.Activate();
        }

        public static void ShowThemeSettings()
        {
            // Zaten açık olan ThemeSettingsWindow varsa öne getir
            foreach (Window w in Current.Windows)
            {
                if (w is ThemeSettingsWindow existing)
                {
                    existing.Activate();
                    return;
                }
            }

            var win = new ThemeSettingsWindow();

            // MainWindow sadece görünürse owner ata — gizliyse owner'sız aç
            var app = Current as App;
            if (app?._mainWindow != null && app._mainWindow.IsVisible)
                win.Owner = app._mainWindow;

            win.Show(); // ShowDialog değil — tray popup kapanınca dialog da kapanmasın
        }

        internal void ShowMainWindow()
        {
            if (_mainWindow == null) return;
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        private static Icon GetIcon()
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                {
                    var ico = Icon.ExtractAssociatedIcon(exePath);
                    if (ico != null) return ico;
                }
            }
            catch { }

            foreach (var uri in new[] { "Assets/icon.ico", "Assets/tray_icon.ico" })
            {
                try
                {
                    var sri = Application.GetResourceStream(
                        new Uri($"pack://application:,,,/{uri}", UriKind.Absolute));
                    if (sri != null) return new Icon(sri.Stream);
                }
                catch { }
            }

            return SystemIcons.Application;
        }
    }
}