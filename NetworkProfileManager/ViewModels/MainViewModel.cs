using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using NetworkProfileManager.Commands;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;

namespace NetworkProfileManager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly NetworkService _networkSvc = new NetworkService();
        private readonly ProfileRepository _profileRepo = new ProfileRepository();
        private readonly HistoryRepository _historyRepo = new HistoryRepository();

        private AdapterViewModel? _selectedAdapter;
        private bool _isLoading;
        private string _statusMessage = Helpers.Loc.Get("Common.Ready", "Hazır");

        public ObservableCollection<AdapterViewModel> Adapters { get; } = new ObservableCollection<AdapterViewModel>();

        public AdapterViewModel? SelectedAdapter
        {
            get => _selectedAdapter;
            set { _selectedAdapter = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand PingCommand { get; }
        public ICommand ApplyProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand RevertCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public MainViewModel()
        {
            RefreshCommand = new RelayCommand(_ => Refresh());
            ApplyCommand = new RelayCommand(
                async _ => await ApplyAsync(),
                _ => SelectedAdapter != null && SelectedAdapter.IsEditValid && !IsLoading);
            PingCommand = new RelayCommand(
                async _ => await DoPingAsync(),
                _ => SelectedAdapter != null && !SelectedAdapter.IsPinging);
            ApplyProfileCommand = new RelayCommand(
                async p => await ApplyProfileAsync(p as ProfileViewModel),
                p => p is ProfileViewModel && !IsLoading);
            DeleteProfileCommand = new RelayCommand(
                p => DeleteProfile(p as ProfileViewModel),
                p => p is ProfileViewModel && !IsLoading);
            RevertCommand = new RelayCommand(
                async p => await RevertAsync(p as HistoryEntry),
                p => p is HistoryEntry h && (h.WasDhcp || !string.IsNullOrEmpty(h.OldIp)) && !IsLoading);
            ClearHistoryCommand = new RelayCommand(
                _ => ClearHistory(),
                _ => SelectedAdapter != null && !IsLoading);

            Refresh();
        }

        // ── Refresh ─────────────────────────────────────────────────
        public void Refresh()
        {
            var selectedName = _selectedAdapter?.Name;
            var adapters = _networkSvc.GetAdapters();

            foreach (var info in adapters)
            {
                var existing = Adapters.FirstOrDefault(a => a.Info.Id == info.Id);
                if (existing != null)
                {
                    existing.UpdateInfo(info);
                }
                else
                {
                    var profiles = _profileRepo.Load(info.Id)
                        .Select(p => new ProfileViewModel(p))
                        .ToList();
                    var history = _historyRepo.Load(info.Id);

                    var vm = new AdapterViewModel(
                        info,
                        new ObservableCollection<ProfileViewModel>(profiles),
                        new ObservableCollection<HistoryEntry>(history));
                    Adapters.Add(vm);
                }
            }

            var ids = adapters.Select(a => a.Id).ToHashSet();
            for (int i = Adapters.Count - 1; i >= 0; i--)
                if (!ids.Contains(Adapters[i].Info.Id))
                    Adapters.RemoveAt(i);

            SelectedAdapter = Adapters.FirstOrDefault(a => a.Name == selectedName)
                           ?? Adapters.FirstOrDefault();

            StatusMessage = Helpers.Loc.Format("Status.Refreshed", DateTime.Now.ToString("HH:mm:ss"));
        }

        // ── Apply static/DHCP ────────────────────────────────────────
        private async Task ApplyAsync()
        {
            if (SelectedAdapter == null) return;

            string oldIp = SelectedAdapter.Info.CurrentIp;
            string oldSubnet = SelectedAdapter.Info.CurrentSubnet;
            string oldGateway = SelectedAdapter.Info.CurrentGateway;
            bool wasDhcp = SelectedAdapter.Info.IsDhcp;
            IsLoading = true;

            try
            {
                // App manifest forces admin at startup — no need to re-elevate per action.
                await Task.Run(() =>
                {
                    if (SelectedAdapter.EditIsDhcp)
                        _networkSvc.ApplyDhcp(SelectedAdapter.Name);
                    else
                        _networkSvc.ApplyStaticIp(
                            SelectedAdapter.Name,
                            SelectedAdapter.EditIp,
                            SelectedAdapter.EditSubnet,
                            SelectedAdapter.EditGateway,
                            SelectedAdapter.EditPrimaryDns,
                            SelectedAdapter.EditSecondaryDns);
                });

                var entry = new HistoryEntry
                {
                    AdapterId = SelectedAdapter.Info.Id,
                    AdapterName = SelectedAdapter.Name,
                    OldIp = oldIp,
                    OldSubnet = oldSubnet,
                    OldGateway = oldGateway,
                    NewIp = SelectedAdapter.EditIsDhcp ? "DHCP" : SelectedAdapter.EditIp,
                    WasDhcp = wasDhcp,
                    IsDhcp = SelectedAdapter.EditIsDhcp,
                    ProfileName = null
                };
                _historyRepo.Append(SelectedAdapter.Info.Id, entry);
                SelectedAdapter.History.Insert(0, entry);

                string msg = SelectedAdapter.EditIsDhcp
                    ? Helpers.Loc.Get("Status.DhcpEnabled", "DHCP etkinleştirildi.")
                    : Helpers.Loc.Format("Status.IpApplied", SelectedAdapter.EditIp);

                StatusMessage = msg;
                NotificationService.Success(msg);
                Refresh();
            }
            catch (Exception ex)
            {
                NotificationService.Error(ex.Message);
                StatusMessage = Helpers.Loc.Format("Status.ErrorPrefix", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Apply profile ────────────────────────────────────────────
        private async Task ApplyProfileAsync(ProfileViewModel? pvm)
        {
            if (pvm == null || SelectedAdapter == null) return;

            string oldIp = SelectedAdapter.Info.CurrentIp;
            string oldSubnet = SelectedAdapter.Info.CurrentSubnet;
            string oldGateway = SelectedAdapter.Info.CurrentGateway;
            bool wasDhcp = SelectedAdapter.Info.IsDhcp;
            IsLoading = true;

            try
            {
                var p = pvm.Model;

                await Task.Run(() =>
                {
                    if (p.IsDhcp)
                        _networkSvc.ApplyDhcp(SelectedAdapter.Name);
                    else
                        _networkSvc.ApplyStaticIp(
                            SelectedAdapter.Name, p.IpAddress, p.SubnetMask, p.Gateway,
                            p.PrimaryDns, p.SecondaryDns);
                });

                var entry = new HistoryEntry
                {
                    AdapterId = SelectedAdapter.Info.Id,
                    AdapterName = SelectedAdapter.Name,
                    OldIp = oldIp,
                    OldSubnet = oldSubnet,
                    OldGateway = oldGateway,
                    NewIp = p.IsDhcp ? "DHCP" : p.IpAddress,
                    WasDhcp = wasDhcp,
                    IsDhcp = p.IsDhcp,
                    ProfileName = p.Name
                };
                _historyRepo.Append(SelectedAdapter.Info.Id, entry);
                SelectedAdapter.History.Insert(0, entry);

                string msg = Helpers.Loc.Format("Status.ProfileApplied", p.Name);
                StatusMessage = msg;
                NotificationService.Success(msg);
                Refresh();
            }
            catch (Exception ex)
            {
                NotificationService.Error(ex.Message);
                StatusMessage = Helpers.Loc.Format("Status.ErrorPrefix", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Delete profile ───────────────────────────────────────────
        private void DeleteProfile(ProfileViewModel? pvm)
        {
            if (pvm == null || SelectedAdapter == null) return;
            SelectedAdapter.Profiles.Remove(pvm);
            _profileRepo.Save(SelectedAdapter.Info.Id,
                SelectedAdapter.Profiles.Select(p => p.Model).ToList());
            NotificationService.Info(Helpers.Loc.Format("Status.ProfileDeleted", pvm.Name));
        }

        // ── Ping ─────────────────────────────────────────────────────
        private async Task DoPingAsync()
        {
            if (SelectedAdapter == null) return;

            string target = SelectedAdapter.PingTarget.Trim();
            if (string.IsNullOrEmpty(target))
            {
                SelectedAdapter.PingResult = Helpers.Loc.Get("Status.PingTargetMissing", "Hedef girilmedi.");
                SelectedAdapter.PingSuccess = false;
                return;
            }

            SelectedAdapter.IsPinging = true;
            SelectedAdapter.PingResult = Helpers.Loc.Format("Ping.Pinging", target);
            SelectedAdapter.PingSuccess = false;

            var (reachable, ms) = await _networkSvc.PingAsync(target);

            SelectedAdapter.IsPinging = false;
            SelectedAdapter.PingSuccess = reachable;
            SelectedAdapter.PingResult = reachable
                ? Helpers.Loc.Format("Status.PingResultMs", target, ms)
                : Helpers.Loc.Format("Status.PingTimeout", target);
        }

        // ── Revert ───────────────────────────────────────────────────
        private async Task RevertAsync(HistoryEntry? entry)
        {
            if (entry == null || SelectedAdapter == null) return;

            IsLoading = true;
            try
            {
                await Task.Run(() =>
                {
                    if (entry.WasDhcp)
                        _networkSvc.ApplyDhcp(SelectedAdapter.Name);
                    else
                        _networkSvc.ApplyStaticIp(
                            SelectedAdapter.Name, entry.OldIp, entry.OldSubnet, entry.OldGateway);
                });

                string msg = entry.WasDhcp
                    ? Helpers.Loc.Get("Status.RevertedDhcp", "DHCP'ye geri alındı.")
                    : Helpers.Loc.Format("Status.RevertedIp", entry.OldIp);
                StatusMessage = msg;
                NotificationService.Success(msg);
                Refresh();
            }
            catch (Exception ex)
            {
                NotificationService.Error(ex.Message);
                StatusMessage = Helpers.Loc.Format("Status.RevertError", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Clear history ────────────────────────────────────────────
        private void ClearHistory()
        {
            if (SelectedAdapter == null) return;
            _historyRepo.Clear(SelectedAdapter.Info.Id);
            SelectedAdapter.History.Clear();
            var cleared = Helpers.Loc.Get("Status.HistoryCleared", "Geçmiş temizlendi.");
            NotificationService.Info(cleared);
            StatusMessage = cleared;
        }

        // ── Update profile ───────────────────────────────────────────
        public void UpdateProfile(ProfileViewModel pvm, IpProfile updated)
        {
            if (SelectedAdapter == null) return;
            int idx = SelectedAdapter.Profiles.IndexOf(pvm);
            if (idx < 0) return;

            SelectedAdapter.Profiles[idx] = new ProfileViewModel(updated);
            SelectedAdapter.RefreshActiveProfiles();
            _profileRepo.Save(SelectedAdapter.Info.Id,
                SelectedAdapter.Profiles.Select(p => p.Model).ToList());

            string msg = Helpers.Loc.Format("Status.ProfileUpdated", updated.Name);
            StatusMessage = msg;
            NotificationService.Success(msg);
        }

        // ── Add profile ──────────────────────────────────────────────
        public void AddProfile(IpProfile profile)
        {
            if (SelectedAdapter == null) return;
            if (SelectedAdapter.Profiles.Count >= 5)
            {
                NotificationService.Warning(Helpers.Loc.Get("Status.MaxProfilesReached",
                    "En fazla 5 profil ekleyebilirsiniz."));
                return;
            }

            SelectedAdapter.Profiles.Add(new ProfileViewModel(profile));
            _profileRepo.Save(SelectedAdapter.Info.Id,
                SelectedAdapter.Profiles.Select(p => p.Model).ToList());

            string msg = Helpers.Loc.Format("Status.ProfileAdded", profile.Name);
            StatusMessage = msg;
            NotificationService.Success(msg);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}