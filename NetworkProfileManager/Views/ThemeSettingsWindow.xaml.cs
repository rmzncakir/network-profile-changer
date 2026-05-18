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
                SelectCurrentTheme();
                SelectCurrentLanguage();
                UpdateSystemLanguageHint();
                TogStartup.IsChecked = StartupHelper.IsStartupEnabled();
                _loaded = true;
            };
        }

        private void SelectCurrentTheme()
        {
            switch (App.CurrentThemeName)
            {
                case "Light":    RbLight.IsChecked    = true; break;
                case "DarkBlue": RbDarkBlue.IsChecked = true; break;
                default:         RbDark.IsChecked     = true; break;
            }
        }

        private void SelectCurrentLanguage()
        {
            switch (LanguageManager.CurrentPreference)
            {
                case LanguageManager.Turkish: RbLangTurkish.IsChecked = true; break;
                case LanguageManager.English: RbLangEnglish.IsChecked = true; break;
                default:                      RbLangSystem.IsChecked  = true; break;
            }
        }

        /// <summary>Show the resolved system language next to the "System" radio.</summary>
        private void UpdateSystemLanguageHint()
        {
            var effective = LanguageManager.ResolveEffective(LanguageManager.SystemDefault);
            LangSystemHint.Text = effective == LanguageManager.Turkish
                ? "— Türkçe"
                : "— English";
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                App.ApplyTheme(tag);
                this.SetResourceReference(BackgroundProperty, "BackgroundBrush");
                this.SetResourceReference(ForegroundProperty, "TextBrush");
            }
        }

        private void Language_Checked(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            if (sender is RadioButton rb && rb.Tag is string tag)
                LanguageManager.ApplyPreference(tag);
        }

        private void StartupToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            StartupHelper.SetStartup(TogStartup.IsChecked == true);
        }

        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
