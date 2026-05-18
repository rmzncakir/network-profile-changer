namespace NetworkProfileManager.Models
{
    public class AdapterInfo
    {
        public string Id          { get; set; } = "";
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";
        public bool   IsConnected { get; set; }
        public string CurrentIp      { get; set; } = "";
        public string CurrentSubnet  { get; set; } = "";
        public string CurrentGateway { get; set; } = "";
        public bool   IsDhcp         { get; set; }
        public string AdapterType    { get; set; } = "";
        public string CurrentPrimaryDns   { get; set; } = "";
        public string CurrentSecondaryDns { get; set; } = "";
    }
}
