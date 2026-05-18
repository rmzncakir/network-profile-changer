using System;

namespace NetworkProfileManager.Models
{
    public class HistoryEntry
    {
        public DateTime Timestamp   { get; set; } = DateTime.Now;
        public string   AdapterId   { get; set; } = "";
        public string   AdapterName { get; set; } = "";
        public string   OldIp       { get; set; } = "";
        public string   OldSubnet   { get; set; } = "";
        public string   OldGateway  { get; set; } = "";
        public string   NewIp       { get; set; } = "";
        public bool     WasDhcp     { get; set; }
        public bool     IsDhcp      { get; set; }
        public string?  ProfileName { get; set; }
    }
}
