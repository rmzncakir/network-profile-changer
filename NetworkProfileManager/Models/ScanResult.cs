using System;

namespace NetworkProfileManager.Models
{
    /// <summary>How a host was proven to exist. Ordered by confidence.</summary>
    public enum DiscoveryMethod
    {
        None = 0,
        /// <summary>Answered an ICMP echo request.</summary>
        Icmp,
        /// <summary>Answered an ARP request — authoritative on the local subnet,
        /// and answered below the firewall, so it finds hosts that drop ICMP.</summary>
        Arp,
        /// <summary>Accepted a TCP connection on a probed port.</summary>
        Tcp
    }

    public class ScanResult
    {
        public string   IpAddress    { get; set; } = "";
        public bool     IsOnline     { get; set; }

        /// <summary>Round-trip time in ms, or -1 when the host never answered ICMP.</summary>
        public long     ResponseTime { get; set; } = -1;

        public string   HostName     { get; set; } = "";
        public string   MacAddress   { get; set; } = "";
        public DateTime ScannedAt    { get; set; } = DateTime.Now;

        /// <summary>Which probe proved the host exists.</summary>
        public DiscoveryMethod FoundBy { get; set; } = DiscoveryMethod.None;

        /// <summary>Most informative TCP port that accepted a connection, 0 if none.</summary>
        public int      OpenPort     { get; set; }

        /// <summary>Every probed port that accepted, comma separated (e.g. "80, 445, 3389").</summary>
        public string   OpenPorts    { get; set; } = "";

        /// <summary>
        /// Protocol hint derived from <see cref="OpenPort"/>. A hint, not a verdict —
        /// one open port is weak evidence of what a device actually is.
        /// </summary>
        public string   DeviceType   { get; set; } = "";

        /// <summary>Compact label for the results grid, e.g. "ARP" or "TCP:502".</summary>
        public string FoundByLabel => FoundBy switch
        {
            DiscoveryMethod.Icmp => "ICMP",
            DiscoveryMethod.Arp  => "ARP",
            DiscoveryMethod.Tcp  => OpenPort > 0 ? $"TCP:{OpenPort}" : "TCP",
            _                    => ""
        };

        /// <summary>Response time for display — blank when the host never answered ICMP.</summary>
        public string ResponseTimeLabel => ResponseTime >= 0 ? ResponseTime.ToString() : "";
    }

    public class ScanProgress
    {
        public int        Current { get; set; }
        public int        Total   { get; set; }
        public ScanResult? Result  { get; set; }
    }

    /// <summary>Tunable scan behaviour. Defaults favour completeness over raw speed.</summary>
    public class ScanOptions
    {
        public int  TimeoutMs    { get; set; } = 1000;

        /// <summary>Worker threads. Higher is faster; each blocks on ARP for absent hosts.</summary>
        public int  MaxParallel  { get; set; } = 128;

        /// <summary>Extra ICMP attempts after the first, for devices that drop the odd packet.</summary>
        public int  IcmpRetries  { get; set; } = 1;

        /// <summary>ARP sweep on the local subnet — the main reason firewalled hosts show up.</summary>
        public bool UseArp       { get; set; } = true;

        /// <summary>TCP connect probe: finds off-subnet hosts and identifies device type.</summary>
        public bool UseTcpProbe  { get; set; } = true;

        public int  TcpTimeoutMs { get; set; } = 400;
    }
}
