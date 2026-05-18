# Network Profile Manager

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D6?logo=windows)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/rmzncakir/network-profile-changer)](../../releases)

Save, switch, and roll back Windows network adapter IP/DNS configurations through a Fluent UI. Up to 5 named profiles per adapter, full history with one-click revert, built-in IP scanner and ping tool.

> **Windows-only.** Uses `netsh` to apply configuration. Requires administrator rights (enforced via app manifest).

---

## Who is this for?

**Field automation / industrial engineers** who hop between PLCs, HMIs, drives and IP cameras every day. Each vendor lives on its own subnet (Siemens `192.168.0.x`, Allen-Bradley `192.168.1.x`, Mitsubishi `192.168.3.x`, Hikvision `192.168.1.64`, …) and switching your laptop's adapter between them is a constant chore.

This app puts the entire flow — **scan the network, ping the target, switch your IP** — into a single window, with **named profiles** so the next time you need that exact static IP / subnet / gateway / DNS combo for a given site, it's one click:

1. **IP Scanner** — sweep a range, find which devices answer, copy their IPs to clipboard or apply directly.
2. **Ping** — verify the device is reachable before doing anything.
3. **Apply** — switch your adapter to a saved Static / DHCP profile in one click. Or save the current settings as a new profile while you're on site.
4. **Revert** — if a change broke connectivity, the last 100 configurations are one click away.

Other typical users: IT field technicians, lab/test-bench engineers, anyone running multiple isolated networks on a single Windows laptop.

---

## Features

- 🌐 **Profile management** — up to 5 named IP profiles per adapter (Static / DHCP, IP, Subnet, Gateway, Primary/Secondary DNS).
- ⚡ **One-click apply** — apply a profile or manual settings to the active adapter instantly.
- ↩️ **Revert from history** — view the last 100 changes and roll back to a previous configuration with one click.
- 🔍 **IP scanner** — discover active devices on a given IP range via ping/ARP, export results to CSV.
- 📡 **Ping tool** — a continuous ping window for connectivity testing.
- 🔔 **Live notifications** — system-tray balloon when adapter IP changes or connection drops.
- 🎨 **3 themes** — Dark (default), Dark Blue, Light.
- 🌍 **Bilingual** — Turkish / English / System (auto-detect). Runtime switching, persisted.
- 🪟 **System tray** — keeps running in the tray when the window is closed; optional auto-start with Windows.

---

## Screenshots

> _Add screenshots here under `docs/screenshots/`._

---

## Download

1. Go to [Releases](../../releases) and grab the latest **`NetworkProfileManager-vX.X.X-win-x64.zip`**.
2. Extract anywhere (e.g. `C:\Tools\NetworkProfileManager\`).
3. Double-click `NetworkProfileManager.exe`. Accept the UAC prompt.

> If Windows SmartScreen shows an "Unknown publisher" warning → **More info → Run anyway.** The executable is currently unsigned.

### Updating

There is **no auto-updater**. Check this repo's [Releases](../../releases) page for new versions, download the latest zip, and extract it over the old folder. Your profiles live in `%LOCALAPPDATA%\NetworkProfileManager\data\` and are preserved across updates.

---

## System requirements

- Windows 10 (build 1809+) or Windows 11
- Administrator rights (required by `netsh` — handled by the app manifest)
- Self-contained build — no .NET runtime install required

---

## Build from source

```powershell
git clone https://github.com/rmzncakir/network-profile-changer.git
cd network-profile-changer
dotnet build -c Release
dotnet publish NetworkProfileManager/NetworkProfileManager.csproj `
    -c Release -r win-x64 --self-contained true -o publish
```

Output is in `publish\`.

---

## Data location

Profiles, history, and theme/language preferences live in:

```
%LOCALAPPDATA%\NetworkProfileManager\data\
├── profiles_<adapter-id>.json
├── history_<adapter-id>.json
└── app_settings.json
```

To uninstall completely, delete the folder above and remove the extracted exe directory.

---

## Security

- **No netsh injection** — every netsh argument is passed via `ProcessStartInfo.ArgumentList`, never via string interpolation.
- **Strict IPv4 validation** — canonical dotted-quad form required; shorthand like `192.168.1` (which `IPAddress.TryParse` accepts as `192.168.0.1`) is rejected to prevent silent misconfiguration.
- **Adapter-name sanitization** — control characters, quotes, CR/LF, and null bytes are rejected.
- **Path-traversal sanitization** — profile/history filenames are sanitized against adapter IDs.
- **One UAC prompt** — the app runs as administrator from launch via manifest; no per-action elevation loop.

To report a vulnerability, see [SECURITY.md](SECURITY.md).

---

## Tests

```powershell
dotnet test NetworkProfileManager.Tests/NetworkProfileManager.Tests.csproj
```

xUnit, **94/94 tests passing**, ~51% line coverage on the testable core (Models, Services, Helpers, Commands, validation logic in ViewModels).

---

## Architecture

```
NetworkProfileManager/
├── Models/         IpProfile, AdapterInfo, HistoryEntry, ScanResult
├── Services/       NetworkService (netsh), Profile/HistoryRepository, IpScannerService, NetworkMonitorService
├── ViewModels/     MainViewModel, AdapterViewModel, ProfileViewModel, IpScannerViewModel
├── Views/          MainWindow, AdapterListPanel, ProfilePanel, HistoryPanel, AddProfileDialog,
│                   IpScannerWindow, PingWindow, ThemeSettingsWindow, TrayPopupWindow
├── Themes/         Dark.xaml, DarkBlue.xaml, Light.xaml
├── Strings/        Tr.xaml, En.xaml — UI strings bound via DynamicResource
├── Helpers/        AppPaths, NotificationService, StartupHelper, LanguageManager, Loc
└── Converters/     BoolToVisibility, StatusToColor, AdapterTypeToSymbol, ...
```

Standard MVVM. UI-independent code (Core) is covered by `NetworkProfileManager.Tests`.

---

## Contributing

PRs welcome. Please:

1. Add tests under `NetworkProfileManager.Tests/` for new code.
2. Make sure `dotnet build` and `dotnet test` pass.
3. Flag any security-relevant change in the PR description.

---

## License

[MIT](LICENSE) © rmzncakir
