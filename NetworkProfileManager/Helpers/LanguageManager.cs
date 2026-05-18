using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;

namespace NetworkProfileManager.Helpers
{
    /// <summary>
    /// Owns the application's current language. Loads a saved preference,
    /// falls back to the system UI culture, and hot-swaps the merged string
    /// ResourceDictionary (Strings/Tr.xaml or Strings/En.xaml).
    /// </summary>
    public static class LanguageManager
    {
        public const string SystemDefault = "System";
        public const string Turkish       = "tr";
        public const string English       = "en";

        private const string SettingsFileName = "app_settings.json";
        private const string LanguageKey      = "Language";

        /// <summary>The user's selected preference (may be "System").</summary>
        public static string CurrentPreference { get; private set; } = SystemDefault;

        /// <summary>The actual culture being shown — "tr" or "en", never "System".</summary>
        public static string EffectiveLanguage { get; private set; } = English;

        /// <summary>Fired after a successful language swap.</summary>
        public static event Action? LanguageChanged;

        /// <summary>
        /// Override for tests — when set, ResolveEffective uses this instead of the OS.
        /// Return "tr" or "en". Set to null to restore default detection.
        /// </summary>
        public static Func<string>? SystemLanguageDetector { get; set; }

        /// <summary>Maps a preference to a concrete language code (System → detect).</summary>
        public static string ResolveEffective(string preference)
        {
            if (preference == Turkish) return Turkish;
            if (preference == English) return English;

            // System detection — uses InstalledUICulture (the OS display language).
            // We deliberately avoid CurrentUICulture because ApplyPreference overrides
            // it; reading from there would lose track of the real system language
            // once the user picks an explicit language and later returns to "System".
            var sys = SystemLanguageDetector?.Invoke()
                      ?? (CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "tr"
                          ? Turkish : English);
            return sys == Turkish ? Turkish : English;
        }

        /// <summary>Call once at startup, before any window opens.</summary>
        public static void Initialize()
        {
            CurrentPreference = LoadSavedPreference() ?? SystemDefault;
            ApplyPreference(CurrentPreference, persist: false);
        }

        /// <summary>Switch language at runtime. Persists the choice.</summary>
        public static void ApplyPreference(string preference, bool persist = true)
        {
            preference = preference switch
            {
                Turkish or English or SystemDefault => preference,
                _ => SystemDefault
            };

            CurrentPreference = preference;
            var effective = ResolveEffective(preference);
            EffectiveLanguage = effective;

            // Set thread culture so DateTime formatting, etc. respect user choice
            var ci = CultureInfo.GetCultureInfo(effective == Turkish ? "tr-TR" : "en-US");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;

            SwapStringsDictionary(effective);

            if (persist) SavePreference(preference);

            LanguageChanged?.Invoke();
        }

        private static void SwapStringsDictionary(string effective)
        {
            if (Application.Current is null) return;

            var newUri = new Uri(
                $"pack://application:,,,/Strings/{(effective == Turkish ? "Tr" : "En")}.xaml",
                UriKind.Absolute);
            var newDict = new ResourceDictionary { Source = newUri };

            var dicts = Application.Current.Resources.MergedDictionaries;

            // Remove any prior Strings/*.xaml dict
            for (int i = dicts.Count - 1; i >= 0; i--)
            {
                if (dicts[i].Source is not null
                    && dicts[i].Source!.OriginalString.IndexOf("/Strings/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    dicts.RemoveAt(i);
                }
            }

            dicts.Add(newDict);
        }

        // ── Persistence ────────────────────────────────────────────

        private static string SettingsPath => Path.Combine(AppPaths.DataDirectory, SettingsFileName);

        private static string? LoadSavedPreference()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return null;
                var json = File.ReadAllText(SettingsPath);
                var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (obj is not null && obj.TryGetValue(LanguageKey, out var v) && !string.IsNullOrEmpty(v))
                    return v;
            }
            catch { }
            return null;
        }

        private static void SavePreference(string preference)
        {
            try
            {
                Dictionary<string, string> obj;
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                          ?? new Dictionary<string, string>();
                }
                else
                {
                    obj = new Dictionary<string, string>();
                }

                obj[LanguageKey] = preference;
                File.WriteAllText(SettingsPath,
                    JsonConvert.SerializeObject(obj, Formatting.Indented));
            }
            catch { /* best-effort */ }
        }
    }
}
