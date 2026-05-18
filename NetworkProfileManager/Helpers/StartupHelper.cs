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

            if (enable)
                key.SetValue(AppName, $"\"{GetExePath()}\" --minimized");
            else
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }

        private static string GetExePath() =>
            Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe");
    }
}