using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Views
{
    public partial class AddProfileDialog : Wpf.Ui.Controls.FluentWindow
    {
        public IpProfile? Result { get; private set; }

        private DialogViewModel _vm;

        public AddProfileDialog()
        {
            _vm = new DialogViewModel();
            InitializeComponent();
            DataContext = _vm;
        }

        public AddProfileDialog(IpProfile existing)
        {
            _vm = new DialogViewModel
            {
                Name         = existing.Name,
                IpAddress    = existing.IpAddress,
                SubnetMask   = existing.SubnetMask,
                Gateway      = existing.Gateway,
                PrimaryDns   = existing.PrimaryDns,
                SecondaryDns = existing.SecondaryDns,
                IsDhcp       = existing.IsDhcp
            };
            InitializeComponent();
            DataContext = _vm;
            Title = Loc.Get("AddProfile.TitleEdit", "Profil Düzenle");
            DialogTitleBar.Title = Loc.Get("AddProfile.TitleEdit", "Profil Düzenle");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.IsValid)
            {
                NotificationService.Warning(
                    Loc.Get("Validation.FillCorrectly", "Lütfen zorunlu alanları doğru doldurun."),
                    Loc.Get("Notify.MissingInfo", "Eksik Bilgi"));
                return;
            }

            Result = new IpProfile
            {
                Name         = _vm.Name.Trim(),
                IpAddress    = _vm.IpAddress.Trim(),
                SubnetMask   = _vm.SubnetMask.Trim(),
                Gateway      = _vm.Gateway.Trim(),
                PrimaryDns   = _vm.PrimaryDns.Trim(),
                SecondaryDns = _vm.SecondaryDns.Trim(),
                IsDhcp       = _vm.IsDhcp
            };

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }

    // ── Dialog ViewModel ────────────────────────────────────────────
    internal class DialogViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _name         = "";
        private string _ipAddress    = "";
        private string _subnetMask   = "255.255.255.0";
        private string _gateway      = "";
        private string _primaryDns   = "";
        private string _secondaryDns = "";
        private bool   _isDhcp;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
        public string SubnetMask
        {
            get => _subnetMask;
            set { _subnetMask = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
        public string Gateway
        {
            get => _gateway;
            set { _gateway = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
        public string PrimaryDns
        {
            get => _primaryDns;
            set { _primaryDns = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
        public string SecondaryDns
        {
            get => _secondaryDns;
            set { _secondaryDns = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }
        public bool IsDhcp
        {
            get => _isDhcp;
            set { _isDhcp = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStatic)); OnPropertyChanged(nameof(IsValid)); }
        }
        public bool IsStatic
        {
            get => !_isDhcp;
            set => IsDhcp = !value;
        }

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name)) return false;
                if (IsDhcp) return true;
                return AdapterViewModel.IsCanonicalIp(IpAddress)
                    && AdapterViewModel.IsCanonicalIp(SubnetMask)
                    && (string.IsNullOrEmpty(Gateway)      || AdapterViewModel.IsCanonicalIp(Gateway))
                    && (string.IsNullOrEmpty(PrimaryDns)   || AdapterViewModel.IsCanonicalIp(PrimaryDns))
                    && (string.IsNullOrEmpty(SecondaryDns) || AdapterViewModel.IsCanonicalIp(SecondaryDns));
            }
        }

        public string Error => "";
        public string this[string col]
        {
            get
            {
                switch (col)
                {
                    case nameof(Name):
                        return string.IsNullOrWhiteSpace(Name)
                            ? Loc.Get("Validation.NameRequired", "İsim zorunludur.")
                            : "";
                    case nameof(IpAddress):
                        if (IsDhcp) return "";
                        return AdapterViewModel.IsCanonicalIp(IpAddress)
                            ? "" : Loc.Get("Validation.InvalidIp", "Geçersiz IP");
                    case nameof(SubnetMask):
                        if (IsDhcp) return "";
                        return AdapterViewModel.IsCanonicalIp(SubnetMask)
                            ? "" : Loc.Get("Validation.InvalidSubnet", "Geçersiz subnet");
                    case nameof(Gateway):
                        if (IsDhcp || string.IsNullOrEmpty(Gateway)) return "";
                        return AdapterViewModel.IsCanonicalIp(Gateway)
                            ? "" : Loc.Get("Validation.InvalidGateway", "Geçersiz gateway");
                    case nameof(PrimaryDns):
                        if (IsDhcp || string.IsNullOrEmpty(PrimaryDns)) return "";
                        return AdapterViewModel.IsCanonicalIp(PrimaryDns)
                            ? "" : Loc.Get("Validation.InvalidDns", "Geçersiz DNS");
                    case nameof(SecondaryDns):
                        if (IsDhcp || string.IsNullOrEmpty(SecondaryDns)) return "";
                        return AdapterViewModel.IsCanonicalIp(SecondaryDns)
                            ? "" : Loc.Get("Validation.InvalidDns", "Geçersiz DNS");
                }
                return "";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
