using System.ComponentModel;
using System.Runtime.CompilerServices;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.ViewModels
{
    public class ProfileViewModel : INotifyPropertyChanged
    {
        public IpProfile Model { get; }

        // Current adapter state — updated by AdapterViewModel
        private string _adapterCurrentIp = "";
        private bool   _adapterIsDhcp;

        public ProfileViewModel(IpProfile model) => Model = model;

        public string Name         => Model.Name;
        public string IpAddress    => Model.IpAddress;
        public string SubnetMask   => Model.SubnetMask;
        public string Gateway      => Model.Gateway;
        public bool   IsDhcp       => Model.IsDhcp;
        public string PrimaryDns   => Model.PrimaryDns;
        public string SecondaryDns => Model.SecondaryDns;

        public string IpDisplay      => Model.IsDhcp ? "DHCP" : Model.IpAddress;
        public string SubnetDisplay  => Model.IsDhcp ? "—"    : Model.SubnetMask;
        public string GatewayDisplay => Model.IsDhcp ? "—"    : (string.IsNullOrEmpty(Model.Gateway) ? "—" : Model.Gateway);

        /// <summary>DNS display: "8.8.8.8 / 8.8.4.4" or single or "—".</summary>
        public string DnsDisplay
        {
            get
            {
                if (Model.IsDhcp) return "Otomatik";
                if (string.IsNullOrEmpty(Model.PrimaryDns)) return "—";
                return string.IsNullOrEmpty(Model.SecondaryDns)
                    ? Model.PrimaryDns
                    : $"{Model.PrimaryDns} / {Model.SecondaryDns}";
            }
        }

        /// <summary>True when this profile matches the adapter's current configuration.</summary>
        public bool IsActive => Model.IsDhcp
            ? _adapterIsDhcp
            : !string.IsNullOrEmpty(Model.IpAddress)
              && string.Equals(Model.IpAddress, _adapterCurrentIp, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Called by AdapterViewModel whenever the adapter's IP / DHCP state changes.</summary>
        public void UpdateFromAdapter(string currentIp, bool isDhcp)
        {
            _adapterCurrentIp = currentIp ?? "";
            _adapterIsDhcp    = isDhcp;
            OnPropertyChanged(nameof(IsActive));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
