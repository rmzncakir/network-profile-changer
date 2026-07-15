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

        /// <summary>
        /// Set by the view to present a Yes/No confirmation dialog for destructive
        /// actions. Returns true when the user confirms. If null (e.g. in tests),
        /// actions proceed without a prompt.
        /// </summary>
        public Func<string, string, Task<bool>>? ConfirmHandler { get; set; }

        public const int MaxProfilesPerAdapter = 5;

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
                async p => await DeleteProfileAsync(p as ProfileViewModel),
                p => p is ProfileViewModel && !IsLoading);
            RevertCommand = new RelayCommand(
                async p => await RevertAsync(p as HistoryEntry),
                p => p is HistoryEntry h && (h.WasDhcp || !string.IsNullOrEmpty(h.OldIp)) && !IsLoading);
            ClearHistoryCommand = new RelayCommand(
                async _ => await ClearHistoryAsync(),
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

        /// <summary>
        /// Shared apply pipeline. Captures the target adapter as a local so a
        /// mid-apply selection change can't redirect the netsh action or record the
        /// history entry against the wrong adapter (the previous per-command reads of
        /// the SelectedAdapter property were racy).
        /// </summary>
        private async Task ApplyCoreAsync(
            AdapterViewModel adapter,
            Action netshAction,
            Func<HistoryEntry>? buildEntry,
            Func<string> successMsg,
            string errorKey = "Status.ErrorPrefix")
        {
            IsLoading = true;
            try
            {
                // App runs asInvoker; NetworkService elevates per apply via UAC
                // (single prompt for the whole netsh batch) when not admin.
                await Task.Run(netshAction);

                if (buildEntry != null)
                {
                    var entry = buildEntry();
                    _historyRepo.Append(adapter.Info.Id, entry);
                    adapter.History.Insert(0, entry);
                }

                string msg = successMsg();
                StatusMessage = msg;
                NotificationService.Success(msg);
                Refresh();
            }
            catch (Exception ex)
            {
                NotificationService.Error(ex.Message);
                StatusMessage = Helpers.Loc.Format(errorKey, ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Apply static/DHCP ────────────────────────────────────────
        private async Task ApplyAsync()
        {
            var adapter = SelectedAdapter;
            if (adapter == null) return;

            // Snapshot everything up front — the user must not be able to mutate
            // these between the UAC prompt and the history record.
            string name = adapter.Name;
            bool editDhcp = adapter.EditIsDhcp;
            string editIp = adapter.EditIp, editSubnet = adapter.EditSubnet, editGw = adapter.EditGateway;
            string editDns1 = adapter.EditPrimaryDns, editDns2 = adapter.EditSecondaryDns;
            string oldIp = adapter.Info.CurrentIp, oldSubnet = adapter.Info.CurrentSubnet;
            string oldGateway = adapter.Info.CurrentGateway;
            bool wasDhcp = adapter.Info.IsDhcp;

            await ApplyCoreAsync(
                adapter,
                () =>
                {
                    if (editDhcp) _networkSvc.ApplyDhcp(name);
                    else _networkSvc.ApplyStaticIp(name, editIp, editSubnet, editGw, editDns1, editDns2);
                },
                () => new HistoryEntry
                {
                    AdapterId = adapter.Info.Id,
                    AdapterName = name,
                    OldIp = oldIp,
                    OldSubnet = oldSubnet,
                    OldGateway = oldGateway,
                    NewIp = editDhcp ? "DHCP" : editIp,
                    WasDhcp = wasDhcp,
                    IsDhcp = editDhcp,
                    ProfileName = null
                },
                () => editDhcp
                    ? Helpers.Loc.Get("Status.DhcpEnabled", "DHCP etkinleştirildi.")
                    : Helpers.Loc.Format("Status.IpApplied", editIp));
        }

        // ── Apply profile ────────────────────────────────────────────
        private async Task ApplyProfileAsync(ProfileViewModel? pvm)
        {
            var adapter = SelectedAdapter;
            if (pvm == null || adapter == null) return;

            string name = adapter.Name;
            var p = pvm.Model;
            string oldIp = adapter.Info.CurrentIp, oldSubnet = adapter.Info.CurrentSubnet;
            string oldGateway = adapter.Info.CurrentGateway;
            bool wasDhcp = adapter.Info.IsDhcp;

            await ApplyCoreAsync(
                adapter,
                () =>
                {
                    if (p.IsDhcp) _networkSvc.ApplyDhcp(name);
                    else _networkSvc.ApplyStaticIp(
                        name, p.IpAddress, p.SubnetMask, p.Gateway, p.PrimaryDns, p.SecondaryDns);
                },
                () => new HistoryEntry
                {
                    AdapterId = adapter.Info.Id,
                    AdapterName = name,
                    OldIp = oldIp,
                    OldSubnet = oldSubnet,
                    OldGateway = oldGateway,
                    NewIp = p.IsDhcp ? "DHCP" : p.IpAddress,
                    WasDhcp = wasDhcp,
                    IsDhcp = p.IsDhcp,
                    ProfileName = p.Name
                },
                () => Helpers.Loc.Format("Status.ProfileApplied", p.Name));
        }

        // ── Delete profile ───────────────────────────────────────────
        private async Task DeleteProfileAsync(ProfileViewModel? pvm)
        {
            var adapter = SelectedAdapter;
            if (pvm == null || adapter == null) return;

            if (!await ConfirmAsync(
                    Helpers.Loc.Get("Confirm.DeleteProfileTitle", "Profili Sil"),
                    Helpers.Loc.Format("Confirm.DeleteProfileMessage", pvm.Name)))
                return;

            adapter.Profiles.Remove(pvm);
            _profileRepo.Save(adapter.Info.Id,
                adapter.Profiles.Select(p => p.Model).ToList());
            NotificationService.Info(Helpers.Loc.Format("Status.ProfileDeleted", pvm.Name));
        }

        private async Task<bool> ConfirmAsync(string title, string message)
        {
            if (ConfirmHandler == null) return true;
            return await ConfirmHandler(title, message);
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
            var adapter = SelectedAdapter;
            if (entry == null || adapter == null) return;

            string name = adapter.Name;
            await ApplyCoreAsync(
                adapter,
                () =>
                {
                    if (entry.WasDhcp) _networkSvc.ApplyDhcp(name);
                    else _networkSvc.ApplyStaticIp(name, entry.OldIp, entry.OldSubnet, entry.OldGateway);
                },
                buildEntry: null,
                () => entry.WasDhcp
                    ? Helpers.Loc.Get("Status.RevertedDhcp", "DHCP'ye geri alındı.")
                    : Helpers.Loc.Format("Status.RevertedIp", entry.OldIp),
                errorKey: "Status.RevertError");
        }

        // ── Clear history ────────────────────────────────────────────
        private async Task ClearHistoryAsync()
        {
            var adapter = SelectedAdapter;
            if (adapter == null) return;

            if (!await ConfirmAsync(
                    Helpers.Loc.Get("Confirm.ClearHistoryTitle", "Geçmişi Temizle"),
                    Helpers.Loc.Get("Confirm.ClearHistoryMessage",
                        "Bu adaptörün tüm geçmiş kayıtları silinecek. Devam edilsin mi?")))
                return;

            _historyRepo.Clear(adapter.Info.Id);
            adapter.History.Clear();
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
            if (SelectedAdapter.Profiles.Count >= MaxProfilesPerAdapter)
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