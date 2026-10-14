using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Views
{
    public partial class ThemeSettingsWindow : Wpf.Ui.Controls.FluentWindow
    {
        private bool _loaded;

        public ThemeSettingsWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                UpdateSystemLanguageLabel();
                Select(CmbTheme, App.CurrentThemeName);
                Select(CmbLanguage, LanguageManager.CurrentPreference);
                TogStartup.IsChecked = StartupHelper.IsStartupEnabled();
                _loaded = true;
            };
        }

        private static void Select(ComboBox box, string tag)
        {
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag)
                               ?? box.Items[0];
        }

        /// <summary>"System language (Türkçe)": show what "System" resolves to.</summary>
        private void UpdateSystemLanguageLabel()
        {
            var effective = LanguageManager.ResolveEffective(LanguageManager.SystemDefault);
            string name = effective == LanguageManager.Turkish ? "Türkçe" : "English";
            LangSystemItem.Content = $"{Loc.Get("Settings.LanguageSystem", "Sistem dili")} ({name})";
        }

        private void Theme_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded) return;
            if (CmbTheme.SelectedItem is ComboBoxItem { Tag: string tag })
                App.ApplyTheme(tag);
        }

        private void Language_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_loaded) return;
            if (!(CmbLanguage.SelectedItem is ComboBoxItem { Tag: string tag })) return;

            LanguageManager.ApplyPreference(tag);

            // A closed ComboBox caches the text of its selected item, so re-select
            // both boxes to show the new language right away.
            _loaded = false;
            UpdateSystemLanguageLabel();
            Reselect(CmbTheme);
            Reselect(CmbLanguage);
            _loaded = true;
        }

        private static void Reselect(ComboBox box)
        {
            var item = box.SelectedItem;
            box.SelectedItem = null;
            box.SelectedItem = item;
        }

        private void StartupToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            StartupHelper.SetStartup(TogStartup.IsChecked == true);
        }

        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
