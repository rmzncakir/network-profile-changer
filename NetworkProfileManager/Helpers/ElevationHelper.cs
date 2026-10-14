using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Newtonsoft.Json;

namespace NetworkProfileManager.Helpers
{
    /// <summary>
    /// Elevation bridge for netsh operations. The app itself stays asInvoker so it
    /// can auto-start with Windows from the HKCU Run key (Windows silently skips
    /// requireAdministrator apps there). When a network change is requested and the
    /// process is not elevated, the whole netsh batch is handed to a hidden, elevated
    /// re-launch of this exe (--run-netsh) — one UAC prompt per apply, not per command.
    /// </summary>
    public static class ElevationHelper
    {
        public const string WorkerSwitch = "--run-netsh";

        private const int Win32ErrorCancelled = 1223; // user declined the UAC prompt

        public static bool IsElevated
        {
            get
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Runs a netsh command batch in an elevated copy of this exe.
        /// Blocks until done; throws with a localized message on cancel or failure.
        /// </summary>
        public static void RunElevated(List<List<string>> commands)
        {
            string requestPath = Path.Combine(Path.GetTempPath(), $"npm_req_{Guid.NewGuid():N}.json");
            string resultPath  = Path.Combine(Path.GetTempPath(), $"npm_res_{Guid.NewGuid():N}.txt");

            try
            {
                File.WriteAllText(requestPath, JsonConvert.SerializeObject(commands));

                var psi = new ProcessStartInfo
                {
                    FileName        = GetExePath(),
                    UseShellExecute = true,
                    Verb            = "runas",
                    WindowStyle     = ProcessWindowStyle.Hidden
                };
                psi.ArgumentList.Add(WorkerSwitch);
                psi.ArgumentList.Add(requestPath);
                psi.ArgumentList.Add(resultPath);

                Process p;
                try
                {
                    p = Process.Start(psi)
                        ?? throw new Exception(Loc.Get("Error.ElevationFailed",
                               "Yönetici yetkisiyle başlatma başarısız oldu."));
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == Win32ErrorCancelled)
                {
                    throw new OperationCanceledException(Loc.Get("Error.ElevationCancelled",
                        "İşlem iptal edildi — yönetici onayı verilmedi."));
                }

                using (p)
                {
                    if (!p.WaitForExit(120_000))
                    {
                        try { p.Kill(); } catch { }
                        throw new Exception(Loc.Get("Error.ElevationTimeout",
                            "Yönetici işlemi zaman aşımına uğradı."));
                    }

                    if (p.ExitCode != 0)
                    {
                        string err = TryReadAllText(resultPath);
                        throw new Exception(string.IsNullOrWhiteSpace(err)
                            ? Loc.Get("Error.ElevationFailed", "Yönetici yetkisiyle başlatma başarısız oldu.")
                            : err);
                    }
                }
            }
            finally
            {
                TryDelete(requestPath);
                TryDelete(resultPath);
            }
        }

        /// <summary>
        /// Entry point for the elevated worker instance (invoked via --run-netsh).
        /// Reads the request file, validates and executes the batch, writes any
        /// error message to the result file. Returns the process exit code.
        /// </summary>
        public static int RunWorker(string requestPath, string resultPath)
        {
            try
            {
                var json = File.ReadAllText(requestPath);
                var commands = JsonConvert.DeserializeObject<List<List<string>>>(json);
                if (commands == null || commands.Count == 0)
                    throw new Exception("Empty request.");

                foreach (var cmd in commands)
                    ValidateCommand(cmd);

                foreach (var cmd in commands)
                    Services.NetworkService.RunNetsh(cmd);

                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(resultPath, ex.Message); } catch { }
                return 1;
            }
        }

        // The only netsh operations the app itself ever issues. Restricting the
        // worker to this exact set (rather than any "interface ip *") minimizes what
        // an attacker could smuggle in via a temp-file race before the elevated read.
        private static readonly HashSet<string> AllowedVerbObject = new(StringComparer.Ordinal)
        {
            "set address",  // static IP or source=dhcp
            "set dns",      // static DNS or source=dhcp
            "add dns",      // secondary DNS
        };

        /// <summary>
        /// The worker only ever accepts a fixed set of "interface ip" subcommands
        /// with clean arguments — it must not become a generic elevated command
        /// runner. Internal so the security boundary can be unit-tested directly.
        /// </summary>
        internal static void ValidateCommand(List<string> cmd)
        {
            if (cmd == null || cmd.Count < 5 || cmd[0] != "interface" || cmd[1] != "ip")
                throw new Exception("Rejected command.");

            if (!AllowedVerbObject.Contains($"{cmd[2]} {cmd[3]}"))
                throw new Exception("Rejected command.");

            foreach (var arg in cmd)
            {
                if (string.IsNullOrEmpty(arg))
                    throw new Exception("Rejected argument.");
                foreach (var ch in arg)
                    if (ch < 0x20 || ch == '"')
                        throw new Exception("Rejected argument.");
            }
        }

        // Environment.ProcessPath is the .exe even in a single-file publish, where
        // Assembly.Location is empty.
        private static string GetExePath() =>
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

        private static string TryReadAllText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path).Trim() : ""; }
            catch { return ""; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
