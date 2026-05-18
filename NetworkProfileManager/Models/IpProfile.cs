using System;

namespace NetworkProfileManager.Models
{
    public class IpProfile
    {
        public string   Id         { get; set; } = Guid.NewGuid().ToString();
        public string   Name       { get; set; } = "";
        public string   IpAddress  { get; set; } = "";
        public string   SubnetMask { get; set; } = "255.255.255.0";
        public string   Gateway    { get; set; } = "";
        public bool     IsDhcp       { get; set; }
        public string   PrimaryDns   { get; set; } = "";
        public string   SecondaryDns { get; set; } = "";
        public DateTime CreatedAt    { get; set; } = DateTime.Now;
    }
}
