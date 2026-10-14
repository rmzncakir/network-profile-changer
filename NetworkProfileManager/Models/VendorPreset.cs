using System.Collections.Generic;

namespace NetworkProfileManager.Models
{
    /// <summary>
    /// A one-click starting point for the edit bar: the address the *laptop* should
    /// take in order to sit on a given vendor's usual subnet.
    ///
    /// These are conventional subnets, not device defaults — the point is to get the
    /// engineer onto the same network as the target quickly, after which the scanner
    /// finds the actual device. Deliberately host .100, which is high enough to avoid
    /// the addresses controllers and gateways typically occupy.
    /// </summary>
    public class VendorPreset
    {
        public string Name       { get; }
        public string IpAddress  { get; }
        public string SubnetMask { get; }
        public string Gateway    { get; }
        public bool   IsDhcp     { get; }

        public VendorPreset(string name, string ip, string mask, string gateway, bool isDhcp = false)
        {
            Name       = name;
            IpAddress  = ip;
            SubnetMask = mask;
            Gateway    = gateway;
            IsDhcp     = isDhcp;
        }

        /// <summary>Shown next to the name so the subnet is visible before applying.</summary>
        public string Description => IsDhcp ? "DHCP" : $"{IpAddress} / {SubnetMask}";

        // Subnets match the vendor table published in the project README. No Beckhoff
        // entry: Beckhoff controllers ship without a fixed address and take whatever
        // they are given, so there is no vendor subnet to preset — save a profile instead.
        public static IReadOnlyList<VendorPreset> All { get; } = new List<VendorPreset>
        {
            new VendorPreset("Siemens S7",        "192.168.0.100", "255.255.255.0", "192.168.0.1"),
            new VendorPreset("Allen-Bradley",     "192.168.1.100", "255.255.255.0", "192.168.1.1"),
            new VendorPreset("Mitsubishi",        "192.168.3.100", "255.255.255.0", "192.168.3.1"),
            new VendorPreset("Hikvision / Dahua", "192.168.1.100", "255.255.255.0", "192.168.1.1"),
            new VendorPreset("Office / DHCP",     "",              "",              "", isDhcp: true),
        };
    }
}
