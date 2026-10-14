using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.ViewModels
{
    public class ProfileViewModel : INotifyPropertyChanged
    {
        public IpProfile Model { get; }

        // Current adapter state — updated by AdapterViewModel
        private AdapterInfo? _adapter;

        // Position in the adapter's list; drives the Ctrl+N label.
        private int _slot = -1;

        public ProfileViewModel(IpProfile model) => Model = model;

        public string Name         => Model.Name;
        public string IpAddress    => Model.IpAddress;
        public string SubnetMask   => Model.SubnetMask;
        public string Gateway      => Model.Gateway;
        public bool   IsDhcp       => Model.IsDhcp;
        public string PrimaryDns   => Model.PrimaryDns;
        public string SecondaryDns => Model.SecondaryDns;

        /// <summary>Short form for the tray menu: the address, or "DHCP".</summary>
        public string IpDisplay => Model.IsDhcp ? "DHCP" : Model.IpAddress;

        /// <summary>
        /// One line for the profile row: "192.168.0.100/24 · GW 192.168.0.1 · DNS 8.8.8.8".
        /// </summary>
        public string Summary
        {
            get
            {
                if (Model.IsDhcp) return "DHCP";

                var parts = new List<string> { Helpers.IpFormat.WithPrefix(Model.IpAddress, Model.SubnetMask) };
                if (!string.IsNullOrEmpty(Model.Gateway))
                    parts.Add($"GW {Model.Gateway}");
                if (!string.IsNullOrEmpty(Model.PrimaryDns))
                    parts.Add(string.IsNullOrEmpty(Model.SecondaryDns)
                        ? $"DNS {Model.PrimaryDns}"
                        : $"DNS {Model.PrimaryDns}, {Model.SecondaryDns}");
                return string.Join("  ·  ", parts);
            }
        }

        /// <summary>"Ctrl+1".."Ctrl+5": the hotkey that applies this profile; empty past the last one.</summary>
        public string ShortcutLabel =>
            _slot >= 0 && _slot < MainViewModel.MaxProfilesPerAdapter ? $"Ctrl+{_slot + 1}" : "";

        internal void SetSlot(int slot)
        {
            if (_slot == slot) return;
            _slot = slot;
            OnPropertyChanged(nameof(ShortcutLabel));
        }

        /// <summary>
        /// True when the adapter already runs everything this profile would set.
        /// Address, mask and gateway must all match: a profile that only shares the
        /// IP would still change the adapter. DNS is compared only when both sides
        /// know it (a disconnected adapter reports none).
        /// </summary>
        public bool IsActive
        {
            get
            {
                var a = _adapter;
                if (a == null) return false;
                if (Model.IsDhcp) return a.IsDhcp;

                return !a.IsDhcp
                    && !string.IsNullOrEmpty(Model.IpAddress)
                    && Same(Model.IpAddress,  a.CurrentIp)
                    && Same(Model.SubnetMask, a.CurrentSubnet)
                    && Same(Model.Gateway,    a.CurrentGateway)
                    && (string.IsNullOrEmpty(Model.PrimaryDns) || string.IsNullOrEmpty(a.CurrentPrimaryDns)
                        || Same(Model.PrimaryDns, a.CurrentPrimaryDns));
            }
        }

        private static bool Same(string? x, string? y)
            => string.Equals((x ?? "").Trim(), (y ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Called by AdapterViewModel whenever the adapter's configuration changes.</summary>
        public void UpdateFromAdapter(AdapterInfo adapter)
        {
            _adapter = adapter;
            OnPropertyChanged(nameof(IsActive));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
