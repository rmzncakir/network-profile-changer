using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Win32;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.Services
{
    public class NetworkService
    {
        public List<AdapterInfo> GetAdapters()
        {
            var result = new List<AdapterInfo>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)   continue;

                bool connected = nic.OperationalStatus == OperationalStatus.Up;
                var props = nic.GetIPProperties();

                // Only IPv4, no loopback
                var unicast = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                     && !IPAddress.IsLoopback(a.Address));
                var gw = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                string ip      = unicast?.Address.ToString() ?? "";
                string subnet  = unicast?.IPv4Mask.ToString() ?? "";
                string gateway = gw?.Address.ToString() ?? "";
                bool   isDhcp  = false;

                try { isDhcp = props.GetIPv4Properties()?.IsDhcpEnabled ?? false; } catch { }

                // DNS servers (IPv4 only)
                var dnsAddrs = props.DnsAddresses
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.ToString())
                    .ToList();
                string primaryDns   = dnsAddrs.Count > 0 ? dnsAddrs[0] : "";
                string secondaryDns = dnsAddrs.Count > 1 ? dnsAddrs[1] : "";

                // Filter APIPA addresses (169.254.x.x) — these appear on disconnected DHCP adapters
                bool isApipa = ip.StartsWith("169.254.");
                if (isApipa) ip = "";

                // If not connected and no active IP, try registry for configured static IP
                if (!connected && string.IsNullOrEmpty(ip))
                {
                    var reg = ReadRegistryIp(nic.Id);
                    ip      = reg.ip;
                    subnet  = reg.subnet;
                    gateway = reg.gateway;
                    isDhcp  = reg.isDhcp;
                }

                result.Add(new AdapterInfo
                {
                    Id                = nic.Id,
                    Name              = nic.Name,
                    Description       = nic.Description,
                    IsConnected       = connected,
                    CurrentIp         = ip,
                    CurrentSubnet     = subnet,
                    CurrentGateway    = gateway,
                    IsDhcp            = isDhcp,
                    AdapterType       = nic.NetworkInterfaceType.ToString(),
                    CurrentPrimaryDns   = primaryDns,
                    CurrentSecondaryDns = secondaryDns
                });
            }

            return result;
        }

        /// <summary>Applies a static IP configuration. Optionally sets DNS servers.</summary>
        public void ApplyStaticIp(string adapterName, string ip, string subnet, string gateway,
                                  string primaryDns = "", string secondaryDns = "")
        {
            EnsureValidAdapterName(adapterName);
            EnsureValidIp(ip,     nameof(ip));
            EnsureValidIp(subnet, nameof(subnet));
            if (!string.IsNullOrWhiteSpace(gateway))
                EnsureValidIp(gateway, nameof(gateway));

            var args = new List<string>
            {
                "interface", "ip", "set", "address",
                $"name={adapterName}",
                "static", ip, subnet
            };
            if (!string.IsNullOrWhiteSpace(gateway))
                args.Add(gateway);

            RunNetsh(args);
            ApplyDns(adapterName, primaryDns, secondaryDns);
        }

        /// <summary>Switches adapter to DHCP mode and resets DNS to automatic.</summary>
        public void ApplyDhcp(string adapterName)
        {
            EnsureValidAdapterName(adapterName);

            RunNetsh(new[] { "interface", "ip", "set", "address",
                             $"name={adapterName}", "source=dhcp" });
            RunNetsh(new[] { "interface", "ip", "set", "dns",
                             $"name={adapterName}", "source=dhcp" });
        }

        /// <summary>Sets primary and optional secondary DNS for a named adapter.</summary>
        public void ApplyDns(string adapterName, string primary, string secondary = "")
        {
            if (string.IsNullOrWhiteSpace(primary)) return;

            EnsureValidAdapterName(adapterName);
            EnsureValidIp(primary, nameof(primary));

            RunNetsh(new[] { "interface", "ip", "set", "dns",
                             $"name={adapterName}", "static", primary, "primary" });

            if (!string.IsNullOrWhiteSpace(secondary))
            {
                EnsureValidIp(secondary, nameof(secondary));
                RunNetsh(new[] { "interface", "ip", "add", "dns",
                                 $"name={adapterName}", secondary, "index=2" });
            }
        }

        // ── Validation (public for testability + defense-in-depth) ─────

        /// <summary>
        /// Validates an IPv4 address in canonical dotted-quad form.
        /// Rejects IPAddress.TryParse's legacy shorthand (e.g. "192.168.1" → 192.168.0.1)
        /// to prevent silent misconfiguration.
        /// </summary>
        public static void EnsureValidIp(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(Loc.Format("Error.InvalidIpv4", paramName), paramName);

            var parts = value.Split('.');
            if (parts.Length != 4)
                throw new ArgumentException(Loc.Format("Error.InvalidIpv4", paramName), paramName);

            foreach (var part in parts)
            {
                if (part.Length == 0 || part.Length > 3) goto invalid;
                if (!byte.TryParse(part, System.Globalization.NumberStyles.None,
                                   System.Globalization.CultureInfo.InvariantCulture, out _))
                    goto invalid;
            }

            if (!IPAddress.TryParse(value, out var addr)
                || addr.AddressFamily != AddressFamily.InterNetwork)
                goto invalid;

            return;

        invalid:
            throw new ArgumentException(Loc.Format("Error.InvalidIpv4", paramName), paramName);
        }

        /// <summary>
        /// Rejects adapter names containing characters that could escape netsh's
        /// argument parsing (control chars, quotes, CR/LF) — defense-in-depth even
        /// though ArgumentList already quotes safely.
        /// </summary>
        public static void EnsureValidAdapterName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    Loc.Get("Error.AdapterNameEmpty", "Adaptör adı boş olamaz."), nameof(name));

            foreach (var ch in name)
            {
                if (ch < 0x20 || ch == '"' || ch == '\r' || ch == '\n' || ch == '\0')
                    throw new ArgumentException(
                        Loc.Get("Error.AdapterNameInvalidChar", "Adaptör adı geçersiz karakter içeriyor."),
                        nameof(name));
            }

            if (name.Length > 256)
                throw new ArgumentException(
                    Loc.Get("Error.AdapterNameTooLong", "Adaptör adı çok uzun."), nameof(name));
        }

        public async Task<(bool reachable, long ms)> PingAsync(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return (false, -1);
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, 2000);
                    return (reply.Status == IPStatus.Success, reply.RoundtripTime);
                }
            }
            catch
            {
                return (false, -1);
            }
        }

        // ── Private helpers ────────────────────────────────────────────

        private static void RunNetsh(IEnumerable<string> args)
        {
            var psi = new ProcessStartInfo("netsh")
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using (var p = Process.Start(psi)
                ?? throw new Exception(Loc.Get("Error.NetshStartFailed", "netsh başlatılamadı.")))
            {
                p.WaitForExit(10_000);
                if (p.ExitCode != 0)
                {
                    var err = p.StandardError.ReadToEnd().Trim();
                    if (string.IsNullOrEmpty(err)) err = p.StandardOutput.ReadToEnd().Trim();
                    throw new Exception(string.IsNullOrEmpty(err)
                        ? Loc.Format("Error.NetshExitCode", p.ExitCode)
                        : err);
                }
            }
        }

        private static (string ip, string subnet, string gateway, bool isDhcp) ReadRegistryIp(string adapterId)
        {
            try
            {
                var keyPath = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{adapterId}";
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) return ("", "", "", true);

                    int enableDhcp = (int)(key.GetValue("EnableDHCP") ?? 1);
                    if (enableDhcp == 1) return ("", "", "", true);

                    var ipArr      = key.GetValue("IPAddress")     as string[];
                    var subnetArr  = key.GetValue("SubnetMask")    as string[];
                    var gwArr      = key.GetValue("DefaultGateway") as string[];

                    string ip      = ipArr?.FirstOrDefault(x => x != "0.0.0.0" && x != "") ?? "";
                    string subnet  = subnetArr?.FirstOrDefault() ?? "";
                    string gateway = gwArr?.FirstOrDefault(x => x != "") ?? "";

                    return (ip, subnet, gateway, false);
                }
            }
            catch
            {
                return ("", "", "", true);
            }
        }

        private static string PrefixToMask(int prefix)
        {
            if (prefix == 0)  return "0.0.0.0";
            if (prefix == 32) return "255.255.255.255";
            uint mask  = ~(uint.MaxValue >> prefix);
            var  bytes = BitConverter.GetBytes(mask);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return string.Join(".", bytes);
        }
    }
}
