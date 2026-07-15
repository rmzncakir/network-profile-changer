using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace NetworkProfileManager.Helpers
{
    /// <summary>
    /// Single owner of app_settings.json (a flat string dictionary holding Theme,
    /// Language, …). Read-modify-write is centralized here so callers can't clobber
    /// each other's keys — previously App.xaml.cs and LanguageManager each carried
    /// their own copy of this dance with comments warning about the coupling.
    /// </summary>
    public static class AppSettingsStore
    {
        private const string FileName = "app_settings.json";

        private static string Path_ =>
            System.IO.Path.Combine(AppPaths.DataDirectory, FileName);

        private static readonly object Gate = new();

        /// <summary>Returns the stored value for <paramref name="key"/>, or null when absent/unreadable.</summary>
        public static string? Get(string key)
        {
            lock (Gate)
            {
                var obj = Load();
                return obj.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
            }
        }

        /// <summary>Merges <paramref name="key"/>=<paramref name="value"/> into the file, preserving other keys.</summary>
        public static void Set(string key, string value)
        {
            lock (Gate)
            {
                var obj = Load();
                obj[key] = value;
                try
                {
                    File.WriteAllText(Path_, JsonConvert.SerializeObject(obj, Formatting.Indented));
                }
                catch { /* best-effort; settings are non-critical */ }
            }
        }

        private static Dictionary<string, string> Load()
        {
            try
            {
                if (File.Exists(Path_))
                {
                    var json = File.ReadAllText(Path_);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();
                }
            }
            catch { /* corrupt file → start fresh; caller's Set will overwrite */ }
            return new Dictionary<string, string>();
        }
    }
}
