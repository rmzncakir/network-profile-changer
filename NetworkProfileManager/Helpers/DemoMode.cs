#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Helpers
{
    /// <summary>
    /// Debug-only fixture for README screenshots: <c>--demo [Light|Dark]</c>.
    /// Invented adapters, profiles, history and scan results replace the real
    /// ones, and every file goes to a throwaway folder, so no real adapter name,
    /// address, MAC or saved profile can end up in a published image. Addresses
    /// are the vendor default subnets; MACs come from the RFC 7042 documentation
    /// range. Not compiled into Release builds.
    /// </summary>
    internal static class DemoMode
    {
        public static bool IsActive { get; private set; }

        private const string EthernetId = "demo-ethernet";
        private const string UsbId      = "demo-usb-ethernet";
        private const string WifiId     = "demo-wifi";

        /// <summary>Activates demo mode when <c>--demo</c> is on the command line.</summary>
        public static bool TryEnable(string[] args)
        {
            int i = Array.IndexOf(args, "--demo");
            if (i < 0) return false;
            IsActive = true;

            var dir = Path.Combine(Path.GetTempPath(), "NetworkProfileManager-demo");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            AppPaths.OverrideDataDirectory(dir);

            AppSettingsStore.Set("Language", "en");
            AppSettingsStore.Set("Theme", i + 1 < args.Length ? args[i + 1] : "Light");
            Seed();
            return true;
        }

        public static List<AdapterInfo> Adapters() => new()
        {
            new AdapterInfo
            {
                Id = EthernetId, Name = "Ethernet", AdapterType = "Ethernet",
                Description = "Intel(R) Ethernet Connection I219-LM",
                IsConnected = true, IsDhcp = false,
                CurrentIp = "192.168.0.100", CurrentSubnet = "255.255.255.0", CurrentGateway = "192.168.0.1",
            },
            new AdapterInfo
            {
                Id = UsbId, Name = "Ethernet 2", AdapterType = "Ethernet",
                Description = "USB 3.0 Gigabit Ethernet Adapter",
                IsConnected = false, IsDhcp = true,
            },
            new AdapterInfo
            {
                Id = WifiId, Name = "Wi-Fi", AdapterType = "Wireless80211",
                Description = "Intel(R) Wi-Fi 6 AX201 160MHz",
                IsConnected = true, IsDhcp = true,
                CurrentIp = "10.20.30.41", CurrentSubnet = "255.255.255.0", CurrentGateway = "10.20.30.1",
                CurrentPrimaryDns = "10.20.30.1",
            },
        };

        /// <summary>Shows a finished scan of the Siemens line subnet.</summary>
        public static void FillScanResults(IpScannerViewModel vm)
        {
            vm.StartIp = "192.168.0.1";
            vm.EndIp   = "192.168.0.254";

            var hosts = new (string Ip, long Ms, string Host, string Mac, DiscoveryMethod By, int Port, string Ports, string Type)[]
            {
                ("192.168.0.1",  1, "plc-line1",     "00-00-5E-00-53-01", DiscoveryMethod.Icmp, 102,   "80, 102",   "S7 / ISO-TSAP (102)"),
                ("192.168.0.2",  2, "hmi-line1",     "00-00-5E-00-53-02", DiscoveryMethod.Icmp, 80,    "80, 443",   "Web (HTTP)"),
                ("192.168.0.10", 1, "",              "00-00-5E-00-53-0A", DiscoveryMethod.Icmp, 502,   "502",       "Modbus TCP (502)"),
                ("192.168.0.11", -1, "",             "00-00-5E-00-53-0B", DiscoveryMethod.Arp,  0,     "",          ""),
                ("192.168.0.20", 3, "io-station-3",  "00-00-5E-00-53-14", DiscoveryMethod.Icmp, 44818, "44818",     "EtherNet/IP (44818)"),
                ("192.168.0.30", 1, "opcua-gateway", "00-00-5E-00-53-1E", DiscoveryMethod.Icmp, 4840,  "443, 4840", "OPC UA (4840)"),
                ("192.168.0.64", 4, "cam-dock-gate", "00-00-5E-00-53-40", DiscoveryMethod.Icmp, 554,   "80, 554",   "RTSP camera (554)"),
                ("192.168.0.90", -1, "",             "00-00-5E-00-53-5A", DiscoveryMethod.Arp,  9100,  "9100",      "Printer (9100)"),
            };

            vm.Results.Clear();
            foreach (var h in hosts)
                vm.Results.Add(new ScanResult
                {
                    IpAddress = h.Ip, IsOnline = true, ResponseTime = h.Ms, HostName = h.Host,
                    MacAddress = h.Mac, FoundBy = h.By, OpenPort = h.Port, OpenPorts = h.Ports, DeviceType = h.Type,
                });
            vm.StatusText = Loc.Format("Status.ScanComplete", hosts.Length);
        }

        private static void Seed()
        {
            new ProfileRepository().Save(EthernetId, new List<IpProfile>
            {
                new() { Name = "Line 1 — S7-1500",        IpAddress = "192.168.0.100", Gateway = "192.168.0.1" },
                new() { Name = "Packaging — CompactLogix", IpAddress = "192.168.1.100" },
                new() { Name = "Robot cell — Mitsubishi",  IpAddress = "192.168.3.100" },
                new() { Name = "Office",                   IsDhcp = true, IpAddress = "", SubnetMask = "" },
            });
            new ProfileRepository().Save(WifiId, new List<IpProfile>
            {
                new() { Name = "Office", IsDhcp = true, IpAddress = "", SubnetMask = "" },
            });

            // Oldest first: Append puts each entry at the top.
            var history = new HistoryRepository();
            var today   = DateTime.Today;
            void Add(DateTime at, string oldIp, string oldGw, bool wasDhcp, string newIp, bool isDhcp, string? profile) =>
                history.Append(EthernetId, new HistoryEntry
                {
                    Timestamp = at, AdapterId = EthernetId, AdapterName = "Ethernet",
                    OldIp = oldIp, OldSubnet = wasDhcp ? "" : "255.255.255.0", OldGateway = oldGw, WasDhcp = wasDhcp,
                    NewIp = newIp, IsDhcp = isDhcp, ProfileName = profile,
                });

            Add(today.AddDays(-1).AddHours(8).AddMinutes(12),  "",              "",            true,  "192.168.1.100", false, "Packaging — CompactLogix");
            Add(today.AddDays(-1).AddHours(11).AddMinutes(47), "192.168.1.100", "",            false, "192.168.3.100", false, "Robot cell — Mitsubishi");
            Add(today.AddDays(-1).AddHours(17).AddMinutes(5),  "192.168.3.100", "",            false, "",              true,  "Office");
            Add(today.AddHours(7).AddMinutes(58),              "",              "",            true,  "192.168.0.100", false, "Line 1 — S7-1500");
        }
    }
}
#endif
