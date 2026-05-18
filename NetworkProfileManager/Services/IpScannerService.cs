using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.Services
{
    public class IpScannerService
    {
        public async Task<List<ScanResult>> ScanRangeAsync(
            string startIp,
            string endIp,
            int timeoutMs       = 500,
            int maxParallel     = 50,
            IProgress<ScanProgress>? progress = null,
            CancellationToken ct = default)
        {
            uint start = ToUInt(startIp);
            uint end   = ToUInt(endIp);
            if (end < start)
                throw new ArgumentException(
                    Loc.Get("Error.EndIpBeforeStart", "Bitiş IP başlangıçtan küçük olamaz."));

            int total   = (int)(end - start + 1);
            int current = 0;
            var bag     = new ConcurrentBag<ScanResult>();
            var sem     = new SemaphoreSlim(maxParallel);
            var tasks   = new List<Task>();

            for (uint ip = start; ip <= end; ip++)
            {
                if (ct.IsCancellationRequested) break;
                uint ipCopy = ip;

                tasks.Add(Task.Run(async () =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var result = await PingOneAsync(ToIpString(ipCopy), timeoutMs, ct)
                            .ConfigureAwait(false);
                        bag.Add(result);
                        int curr = Interlocked.Increment(ref current);
                        progress?.Report(new ScanProgress { Current = curr, Total = total, Result = result });
                    }
                    finally { sem.Release(); }
                }, ct));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return bag.OrderBy(r => ToUInt(r.IpAddress)).ToList();
        }

        private static async Task<ScanResult> PingOneAsync(
            string ip, int timeoutMs, CancellationToken ct)
        {
            var result = new ScanResult { IpAddress = ip, ScannedAt = DateTime.Now };
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, timeoutMs).ConfigureAwait(false);
                    result.IsOnline     = reply.Status == IPStatus.Success;
                    result.ResponseTime = reply.RoundtripTime;
                }

                if (result.IsOnline)
                {
                    result.HostName   = await ResolveHostAsync(ip).ConfigureAwait(false);
                    result.MacAddress = GetMacFromArp(ip);
                }
            }
            catch { }
            return result;
        }

        private static async Task<string> ResolveHostAsync(string ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip).ConfigureAwait(false);
                return entry.HostName;
            }
            catch { return ""; }
        }

        private static string GetMacFromArp(string ip)
        {
            try
            {
                var psi = new ProcessStartInfo("arp", $"-a {ip}")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return "";
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    var match = Regex.Match(output,
                        @"([0-9A-Fa-f]{2}[:\-][0-9A-Fa-f]{2}[:\-][0-9A-Fa-f]{2}" +
                        @"[:\-][0-9A-Fa-f]{2}[:\-][0-9A-Fa-f]{2}[:\-][0-9A-Fa-f]{2})");
                    return match.Success ? match.Value.ToUpperInvariant() : "";
                }
            }
            catch { return ""; }
        }

        private static uint ToUInt(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) throw new FormatException($"Geçersiz IP: {ip}");
            return ((uint)int.Parse(parts[0]) << 24)
                 | ((uint)int.Parse(parts[1]) << 16)
                 | ((uint)int.Parse(parts[2]) <<  8)
                 |  (uint)int.Parse(parts[3]);
        }

        private static string ToIpString(uint ip)
            => $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
    }
}
