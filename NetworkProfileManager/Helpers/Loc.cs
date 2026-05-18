using System;
using System.Globalization;
using System.Windows;

namespace NetworkProfileManager.Helpers
{
    /// <summary>
    /// String resource lookup against the currently merged language ResourceDictionary
    /// (Strings/Tr.xaml or Strings/En.xaml). Used by code-behind / ViewModels;
    /// XAML uses <c>{DynamicResource Key}</c> directly.
    /// </summary>
    public static class Loc
    {
        /// <summary>Look up a string by key; returns <paramref name="fallback"/> when not found.</summary>
        public static string Get(string key, string? fallback = null)
        {
            if (Application.Current?.TryFindResource(key) is string s) return s;
            return fallback ?? key;
        }

        /// <summary>Look up a format string by key and interpolate args (invariant formatter).</summary>
        public static string Format(string key, params object?[] args)
        {
            var fmt = Get(key, key);
            try { return string.Format(CultureInfo.InvariantCulture, fmt, args); }
            catch (FormatException) { return fmt; }
        }
    }
}
