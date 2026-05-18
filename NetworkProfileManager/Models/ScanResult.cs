using System;

namespace NetworkProfileManager.Models
{
    public class ScanResult
    {
        public string   IpAddress    { get; set; } = "";
        public bool     IsOnline     { get; set; }
        public long     ResponseTime { get; set; }
        public string   HostName     { get; set; } = "";
        public string   MacAddress   { get; set; } = "";
        public DateTime ScannedAt    { get; set; } = DateTime.Now;
    }

    public class ScanProgress
    {
        public int        Current { get; set; }
        public int        Total   { get; set; }
        public ScanResult? Result  { get; set; }
    }
}
