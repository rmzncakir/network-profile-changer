using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace NetworkProfileManager.Helpers
{
    public static class StartupHelper
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NetworkProfileManager";

        public static bool IsStartupEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) != null;
        }

        public static void SetStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            // Always remove the existing entry first so a stale path from an
            // older install never lingers; enabling re-adds it fresh from the
            // currently running exe.
            key.DeleteValue(AppName, throwOnMissingValue: false);
            if (enable)
                key.SetValue(AppName, BuildRunCommand());
        }

        /// <summary>
        /// If the Run entry exists but points to a different exe than the one
        /// currently running (e.g. user moved the install folder), rewrite it
        /// so startup keeps working from the new location. No-op when disabled.
        /// </summary>
        public static void RefreshIfEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            var existing = key.GetValue(AppName) as string;
            if (string.IsNullOrEmpty(existing)) return;

            var current = BuildRunCommand();
            if (!string.Equals(existing, current, StringComparison.OrdinalIgnoreCase))
                key.SetValue(AppName, current);
        }

        private static string BuildRunCommand() => $"\"{GetExePath()}\" --minimized";

        private static string GetExePath() =>
            Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe");
    }
}