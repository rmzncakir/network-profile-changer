using System;
using System.Windows;
using System.Windows.Media.Animation;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.ViewModels;
using NetworkProfileManager.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NetworkProfileManager
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly SnackbarService        _snackbarService = new();
        private readonly ContentDialogService   _dialogService   = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            // Fix maximized window overlapping the taskbar
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Maximized)
                {
                    var area = SystemParameters.WorkArea;
                    MaxWidth  = area.Width;
                    MaxHeight = area.Height;
                }
                else
                {
                    MaxWidth  = double.PositiveInfinity;
                    MaxHeight = double.PositiveInfinity;
                }
            };
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Wire up WPF-UI services
            _snackbarService.SetSnackbarPresenter(RootSnackbar);
            _dialogService.SetDialogHost(RootDialogHost);
            NotificationService.Initialize(_snackbarService);

            // Startup slide+fade animation
            Opacity = 0;
            var origTop = Top;
            Top = origTop + 18;
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(280))));
            BeginAnimation(TopProperty,
                new DoubleAnimation(origTop + 18, origTop,
                    new Duration(TimeSpan.FromMilliseconds(280)))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        }

        // ── Theme settings ───────────────────────────────────────────
        private void OpenThemeClick(object sender, RoutedEventArgs e)
        {
            var win = new ThemeSettingsWindow { Owner = this };
            win.ShowDialog();
        }

        // ── Ping window ──────────────────────────────────────────────
        private void OpenPingWindowClick(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.SelectedAdapter == null) return;

            string target = vm.SelectedAdapter.PingTarget.Trim();
            if (string.IsNullOrEmpty(target)) target = vm.SelectedAdapter.Info.CurrentGateway;
            if (string.IsNullOrEmpty(target)) return;

            new PingWindow(target) { Owner = this }.Show();
        }

        // ── IP Scanner window ────────────────────────────────────────
        private void OpenScannerClick(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            string adapterIp = vm?.SelectedAdapter?.Info.CurrentIp ?? "";

            string start = "", end = "";
            if (System.Net.IPAddress.TryParse(adapterIp, out var parsed))
            {
                var b = parsed.GetAddressBytes();
                start = $"{b[0]}.{b[1]}.{b[2]}.1";
                end   = $"{b[0]}.{b[1]}.{b[2]}.254";
            }

            new IpScannerWindow(start, end, ip =>
            {
                if (vm?.SelectedAdapter == null) return;
                vm.SelectedAdapter.EditIp       = ip;
                vm.SelectedAdapter.EditIsStatic = true;
            }) { Owner = this }.Show();
        }

        // ── Right panel fade-in on adapter change ────────────────────
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(DataContext is MainViewModel vm)) return;
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.SelectedAdapter))
                    FadeIn(RightPanel);
            };
        }

        private static void FadeIn(UIElement el)
        {
            el.Opacity = 0;
            el.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200))));
        }
    }
}
