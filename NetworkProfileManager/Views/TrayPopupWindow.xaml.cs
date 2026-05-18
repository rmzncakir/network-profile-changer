using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using NetworkProfileManager.ViewModels;
using Wpf.Ui.Controls;
using SWC = System.Windows.Controls;

namespace NetworkProfileManager.Views
{
    public partial class TrayPopupWindow : Window
    {
        public Action? OnBeforeClose { get; set; }
        private bool _isClosing;

        public TrayPopupWindow()
        {
            InitializeComponent();
            ContentRendered += (s, e) => PositionNearTaskbar();
            Deactivated += (s, e) => ClosePopup();
        }

        private void ClosePopup()
        {
            if (_isClosing) return;
            _isClosing = true;
            OnBeforeClose?.Invoke();
            Close();
        }

        // ── Build / rebuild content ──────────────────────────────────
        public void Rebuild(MainViewModel? vm, Action openMain, Action openSettings, Action exit)
        {
            ItemsPanel.Children.Clear();

            AddAction(Helpers.Loc.Get("Tray.OpenMain", "Pencereyi aç"), SymbolRegular.WindowNew20, openMain);

            if (vm != null)
            {
                bool anyProfiles = false;
                foreach (var adapter in vm.Adapters)
                {
                    if (adapter.Profiles.Count == 0) continue;
                    if (!anyProfiles)
                    {
                        AddSeparator();
                        anyProfiles = true;
                    }

                    AddAdapterLabel(adapter.Name);

                    foreach (var profile in adapter.Profiles)
                    {
                        var adp = adapter;
                        var pvm = profile;

                        string ipDisplay = pvm.Model.IsDhcp
                            ? "DHCP"
                            : pvm.Model.IpAddress;

                        AddProfileItem(pvm.Name, ipDisplay, pvm.IsActive, () =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                vm.SelectedAdapter = adp;
                                vm.ApplyProfileCommand.Execute(pvm);
                            });
                        });
                    }
                }
            }

            AddSeparator();
            AddAction(Helpers.Loc.Get("Tray.OpenSettings", "Ayarlar"), SymbolRegular.Settings20, openSettings);
            AddSeparator();
            AddAction(Helpers.Loc.Get("Tray.Exit", "Çıkış"), SymbolRegular.Dismiss20, exit);
        }

        // ── Item helpers ─────────────────────────────────────────────
        private void AddAction(string text, SymbolRegular symbol, Action onClick)
        {
            var icon = new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 15,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(ForegroundProperty, "SubtextBrush");

            var label = new SWC.TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.SetResourceReference(ForegroundProperty, "TextBrush");

            var sp = new SWC.StackPanel { Orientation = SWC.Orientation.Horizontal };
            sp.Children.Add(icon);
            sp.Children.Add(label);

            var btn = new SWC.Button
            {
                Content = sp,
                Style = (Style)FindResource("TrayActionBtn")
            };
            btn.Click += (s, e) => { ClosePopup(); onClick(); };
            ItemsPanel.Children.Add(btn);
        }

        private void AddAdapterLabel(string name)
        {
            var tb = new SWC.TextBlock
            {
                Text = name,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(14, 6, 14, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            tb.SetResourceReference(ForegroundProperty, "SubtextBrush");
            ItemsPanel.Children.Add(tb);
        }

        private void AddProfileItem(string name, string ipDisplay, bool isActive, Action onClick)
        {
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Margin = new Thickness(0, 2, 8, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = isActive ? Visibility.Visible : Visibility.Collapsed
            };
            dot.SetResourceReference(Shape.FillProperty, "AccentBrush");

            var nameBlock = new SWC.TextBlock
            {
                Text = name,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12
            };
            nameBlock.SetResourceReference(ForegroundProperty, "TextBrush");

            var ipBlock = new SWC.TextBlock
            {
                Text = ipDisplay,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 1, 0, 0),
                Visibility = string.IsNullOrEmpty(ipDisplay)
                                 ? Visibility.Collapsed
                                 : Visibility.Visible
            };
            ipBlock.SetResourceReference(ForegroundProperty, "SubtextBrush");

            var textStack = new SWC.StackPanel { Orientation = SWC.Orientation.Vertical };
            textStack.Children.Add(nameBlock);
            textStack.Children.Add(ipBlock);

            var sp = new SWC.StackPanel
            {
                Orientation = SWC.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(dot);
            sp.Children.Add(textStack);

            var btn = new SWC.Button
            {
                Content = sp,
                Style = (Style)FindResource("TrayProfileBtn")
            };
            btn.Click += (s, e) => { ClosePopup(); onClick(); };
            ItemsPanel.Children.Add(btn);
        }

        private void AddSeparator()
        {
            var rect = new Rectangle { Height = 1, Margin = new Thickness(8, 4, 8, 4) };
            rect.SetResourceReference(Shape.FillProperty, "BorderBrush");
            ItemsPanel.Children.Add(rect);
        }

        // ── Positioning ──────────────────────────────────────────────
        private void PositionNearTaskbar()
        {
            var area = SystemParameters.WorkArea;

            MaxHeight = area.Height * 0.80;

            Left = area.Right - ActualWidth - 4;
            Top = area.Bottom - ActualHeight + 6;
        }
    }
}