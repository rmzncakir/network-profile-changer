using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;
using NetworkProfileManager.ViewModels;
using NetworkProfileManager.Views;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace NetworkProfileManager
{
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private TrayPopupWindow? _trayPopup;
        private NetworkMonitorService? _networkMonitor;
        private DateTime _popupLastClosed = DateTime.MinValue;
        private bool _trayHintShown;

        internal bool IsExiting { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Elevated netsh worker mode (--run-netsh <request> <result>):
            // launched by ElevationHelper via UAC. No UI, no tray — run the
            // batch and exit with the worker's result code.
            if (e.Args.Length >= 3 && e.Args[0] == ElevationHelper.WorkerSwitch)
            {
                int code = ElevationHelper.RunWorker(e.Args[1], e.Args[2]);
                Environment.Exit(code);
                return;
            }

            base.OnStartup(e);

            // Migrate legacy data (exe-relative) into LocalAppData on first run.
            // Must run BEFORE LanguageManager.Initialize so the language preference
            // saved by an older version is picked up correctly.
            NetworkProfileManager.Helpers.AppPaths.MigrateLegacyDataIfPresent();

            // Localization must initialize BEFORE any window is constructed so
            // DynamicResource lookups resolve to the right culture.
            LanguageManager.Initialize();

            // If startup is enabled but the registered path no longer matches
            // the current exe (e.g. folder moved), self-heal. Never silently
            // re-enable startup the user disabled.
            StartupHelper.RefreshIfEnabled();

            bool startMinimized = Array.Exists(e.Args, a => a == "--minimized");

            var savedTheme = LoadSavedTheme();
            ApplyTheme(savedTheme);
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

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

            // Watch adapter up/down/IP-change and surface tray balloons.
            if (_trayIcon != null)
                _networkMonitor = new NetworkMonitorService(_trayIcon);

            _mainWindow = new MainWindow();
            _mainWindow.Closing += (s, args) =>
            {
                if (!IsExiting)
                {
                    args.Cancel = true;
                    _mainWindow!.Hide();
                    ShowTrayHintOnce();
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
            // Static event: detach or the handler outlives the app.
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _networkMonitor?.Dispose();
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        // First time the window is hidden to the tray, tell the user it's still
        // running there — otherwise closing looks like a full exit.
        private void ShowTrayHintOnce()
        {
            if (_trayHintShown || _trayIcon == null) return;
            _trayHintShown = true;

            _trayIcon.BalloonTipIcon  = ToolTipIcon.Info;
            _trayIcon.BalloonTipTitle = Loc.Get("Tray.HintTitle", "Arka planda çalışıyor");
            _trayIcon.BalloonTipText  = Loc.Get("Tray.HintText",
                "Uygulama sistem tepsisinde çalışmaya devam ediyor. Çıkmak için tepsi simgesine sağ tıklayın.");
            _trayIcon.ShowBalloonTip(4000);
        }

        // ── Theme persistence ─────────────────────────────────────────
        private static string LoadSavedTheme() => NormalizeTheme(AppSettingsStore.Get("Theme"));

        private static void SaveTheme(string themeName) => AppSettingsStore.Set("Theme", themeName);

        // Older versions also offered "DarkBlue"; it folds into Dark. Anything
        // unknown (or nothing saved yet) follows Windows.
        internal static string NormalizeTheme(string? themeName) => themeName switch
        {
            "Light"              => "Light",
            "Dark" or "DarkBlue" => "Dark",
            _                    => "System",
        };

        // ── Theme ─────────────────────────────────────────────────────
        /// <summary>"System" follows Windows' light/dark mode; "Light" and "Dark" pin it.</summary>
        public static string CurrentThemeName { get; private set; } = "System";

        public static void ApplyTheme(string themeName)
        {
            CurrentThemeName = NormalizeTheme(themeName);
            ApplyCurrentTheme();
            SaveTheme(CurrentThemeName);
        }

        private static void ApplyCurrentTheme()
        {
            // Both calls also pick up the Windows accent colour.
            if (CurrentThemeName == "System")
                ApplicationThemeManager.ApplySystemTheme(updateAccent: true);
            else
                ApplicationThemeManager.Apply(
                    CurrentThemeName == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark,
                    WindowBackdropType.Mica,
                    updateAccent: true);

            // WPF-UI re-tints the main window and the windows it owns. Windows
            // opened from the tray have no owner, so refresh their Mica here.
            var theme = ApplicationThemeManager.GetAppTheme();
            foreach (Window w in Current.Windows)
            {
                if (w is FluentWindow fw && w != Current.MainWindow && w.Owner == null)
                    WindowBackgroundManager.UpdateBackground(fw, theme, fw.WindowBackdropType);
            }
        }

        // Windows raises this when the user switches light/dark mode or the
        // accent colour. The category is broad, so only re-apply on a real change.
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;

            Dispatcher.BeginInvoke(() =>
            {
                // WPF-UI caches the Windows theme and only refreshes it inside
                // Apply, so read it fresh or a light/dark switch goes unnoticed.
                SystemThemeManager.UpdateSystemThemeCache();

                bool accentChanged = ApplicationAccentColorManager.GetColorizationColor()
                                     != ApplicationAccentColorManager.SystemAccent;
                if (NeedsThemeRefresh(CurrentThemeName,
                                      ApplicationThemeManager.GetAppTheme(),
                                      ApplicationThemeManager.GetSystemTheme(),
                                      accentChanged))
                    ApplyCurrentTheme();
            });
        }

        /// <summary>
        /// Whether a Windows settings change needs the theme re-applied: always for a
        /// new accent colour, and for "System" when Windows now wants another theme
        /// than the one showing. Mirrors WPF-UI's ApplySystemTheme mapping.
        /// </summary>
        internal static bool NeedsThemeRefresh(string themeName, ApplicationTheme appTheme,
                                               SystemTheme systemTheme, bool accentChanged)
        {
            if (accentChanged) return true;
            if (themeName != "System") return false;

            var wanted = systemTheme switch
            {
                SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow => ApplicationTheme.Dark,
                SystemTheme.HC1 or SystemTheme.HC2 or SystemTheme.HCBlack or SystemTheme.HCWhite
                    => ApplicationTheme.HighContrast,
                _ => ApplicationTheme.Light,
            };
            return wanted != appTheme;
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