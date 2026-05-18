using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.ViewModels
{
    public class AdapterViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private AdapterInfo _info;
        private string _editIp           = "";
        private string _editSubnet       = "255.255.255.0";
        private string _editGateway      = "";
        private string _editPrimaryDns   = "";
        private string _editSecondaryDns = "";
        private bool   _editIsDhcp;
        private string _pingTarget  = "";
        private string _pingResult  = "";
        private bool   _pingSuccess;
        private bool   _isPinging;

        public AdapterViewModel(AdapterInfo info,
                                ObservableCollection<ProfileViewModel> profiles,
                                ObservableCollection<HistoryEntry> history)
        {
            _info    = info;
            Profiles = profiles;
            History  = history;
            LoadFromInfo();
            NotifyProfilesActive();
        }

        // ── Adapter info ────────────────────────────────────────────
        public AdapterInfo Info => _info;

        public void UpdateInfo(AdapterInfo info)
        {
            _info = info;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(CurrentIp));
            OnPropertyChanged(nameof(IsDhcp));
            OnPropertyChanged(nameof(CurrentDnsDisplay));
            NotifyProfilesActive();
        }

        public void RefreshActiveProfiles() => NotifyProfilesActive();

        // Tell each profile whether it is currently "active" on this adapter
        private void NotifyProfilesActive()
        {
            foreach (var p in Profiles)
                p.UpdateFromAdapter(_info.CurrentIp, _info.IsDhcp);
        }

        public string Name        => _info.Name;
        public string Description => _info.Description;
        public bool   IsConnected => _info.IsConnected;
        public string CurrentIp   => string.IsNullOrEmpty(_info.CurrentIp) ? "—" : _info.CurrentIp;
        public bool   IsDhcp      => _info.IsDhcp;
        public string AdapterType => _info.AdapterType;

        public string CurrentDnsDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(_info.CurrentPrimaryDns)) return "";
                return string.IsNullOrEmpty(_info.CurrentSecondaryDns)
                    ? _info.CurrentPrimaryDns
                    : $"{_info.CurrentPrimaryDns} / {_info.CurrentSecondaryDns}";
            }
        }

        // ── Collections ─────────────────────────────────────────────
        public ObservableCollection<ProfileViewModel> Profiles { get; }
        public ObservableCollection<HistoryEntry>     History  { get; }

        // ── Edit form ────────────────────────────────────────────────
        public string EditIp
        {
            get => _editIp;
            set { _editIp = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditValid)); }
        }

        public string EditSubnet
        {
            get => _editSubnet;
            set { _editSubnet = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditValid)); }
        }

        public string EditGateway
        {
            get => _editGateway;
            set
            {
                _editGateway = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditValid));
                if (string.IsNullOrEmpty(_pingTarget) || _pingTarget == _info.CurrentGateway)
                    PingTarget = value;
            }
        }

        public bool EditIsDhcp
        {
            get => _editIsDhcp;
            set
            {
                _editIsDhcp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EditIsStatic));
                OnPropertyChanged(nameof(IsEditValid));
            }
        }

        public bool EditIsStatic
        {
            get => !_editIsDhcp;
            set => EditIsDhcp = !value;
        }

        public string EditPrimaryDns
        {
            get => _editPrimaryDns;
            set { _editPrimaryDns = value; OnPropertyChanged(); }
        }

        public string EditSecondaryDns
        {
            get => _editSecondaryDns;
            set { _editSecondaryDns = value; OnPropertyChanged(); }
        }

        // ── Ping ────────────────────────────────────────────────────
        public string PingTarget
        {
            get => _pingTarget;
            set { _pingTarget = value; OnPropertyChanged(); }
        }

        public string PingResult
        {
            get => _pingResult;
            set { _pingResult = value; OnPropertyChanged(); }
        }

        public bool PingSuccess
        {
            get => _pingSuccess;
            set { _pingSuccess = value; OnPropertyChanged(); }
        }

        public bool IsPinging
        {
            get => _isPinging;
            set { _isPinging = value; OnPropertyChanged(); }
        }

        // ── Validation ───────────────────────────────────────────────
        public bool IsEditValid
        {
            get
            {
                if (EditIsDhcp) return true;
                return IsCanonicalIp(EditIp)
                    && IsCanonicalIp(EditSubnet)
                    && (string.IsNullOrEmpty(EditGateway) || IsCanonicalIp(EditGateway));
            }
        }

        // IDataErrorInfo — drives red border on TextBoxes
        public string Error => "";

        public string this[string columnName]
        {
            get
            {
                if (EditIsDhcp) return "";
                switch (columnName)
                {
                    case nameof(EditIp):
                        return IsCanonicalIp(EditIp)
                            ? ""
                            : Helpers.Loc.Get("Validation.InvalidIp", "Geçersiz IP adresi");
                    case nameof(EditSubnet):
                        return IsCanonicalIp(EditSubnet)
                            ? ""
                            : Helpers.Loc.Get("Validation.InvalidSubnet", "Geçersiz subnet maskesi");
                    case nameof(EditGateway):
                        if (string.IsNullOrEmpty(EditGateway)) return "";
                        return IsCanonicalIp(EditGateway)
                            ? ""
                            : Helpers.Loc.Get("Validation.InvalidGateway", "Geçersiz gateway");
                }
                return "";
            }
        }

        /// <summary>
        /// Canonical IPv4 dotted-quad — rejects legacy shorthand
        /// (e.g. "192.168.1" → 192.168.0.1) that IPAddress.TryParse accepts.
        /// Mirrors NetworkService.EnsureValidIp.
        /// </summary>
        public static bool IsCanonicalIp(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parts = value.Split('.');
            if (parts.Length != 4) return false;
            foreach (var part in parts)
            {
                if (part.Length == 0 || part.Length > 3) return false;
                if (!byte.TryParse(part, System.Globalization.NumberStyles.None,
                                   System.Globalization.CultureInfo.InvariantCulture, out _))
                    return false;
            }
            return IPAddress.TryParse(value, out var addr)
                && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        // ── Helpers ──────────────────────────────────────────────────
        public void LoadFromInfo()
        {
            EditIp           = _info.CurrentIp;
            EditSubnet       = _info.CurrentSubnet;
            EditGateway      = _info.CurrentGateway;
            EditPrimaryDns   = _info.CurrentPrimaryDns;
            EditSecondaryDns = _info.CurrentSecondaryDns;
            EditIsDhcp       = _info.IsDhcp;
            PingTarget       = _info.CurrentGateway;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
