# Network Profile Manager

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D6?logo=windows)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Save, switch, and roll back Windows network adapter IP/DNS configurations from a Fluent UI.
Adaptör başına 5 profil saklar, geçmişten geri alma yapar, IP tarayıcı içerir.

> **Windows-only.** Uses `netsh` to apply configuration. Requires administrator rights (enforced via app manifest).

---

## Özellikler

- 🌐 **Profil yönetimi** — adaptör başına 5'e kadar isimli IP profili (Statik / DHCP, IP, Subnet, Gateway, Primary/Secondary DNS).
- ⚡ **Tek tıkla uygulama** — profili veya manuel ayarları aktif adaptöre anında uygula.
- ↩️ **Geçmişten geri alma** — son 100 değişikliği görüntüle ve önceki yapılandırmaya tek tuşla dön.
- 🔍 **IP tarayıcı** — verilen aralıkta ping/ARP ile aktif cihazları bul, CSV'ye dışa aktar.
- 📡 **Ping aracı** — sürekli ping penceresi ile bağlantı testi.
- 🔔 **Otomatik bildirim** — adaptör IP'si değişince ya da bağlantı kesilince system tray balloon.
- 🎨 **3 tema** — Koyu (varsayılan), Koyu Mavi, Açık.
- 🌍 **Çift dil** — Türkçe / English / Sistem dili (otomatik algılama). Runtime'da değişir, kaydedilir.
- 🪟 **System tray** — pencere kapatıldığında tepside çalışmaya devam eder; Windows başlangıcında otomatik açılış desteği.

---

## Ekran Görüntüleri

> _Buraya `docs/screenshots/` altından ana pencere + profil dialog + IP tarayıcı ekran görüntülerini ekle._

---

## Sistem Gereksinimleri

- Windows 10 (sürüm 1809+) veya Windows 11
- .NET 8 Desktop Runtime (kurulum sırasında otomatik tetiklenir)
- Yönetici hakları (netsh için zorunlu — manifest tarafından kontrol edilir)

## Kurulum

### Yayınlanmış sürüm

1. [Releases](../../releases) sayfasından **son `NetworkProfileManager-vX.X.X-win-x64.zip`** dosyasını indir.
2. İstediğin klasöre çıkar (örn. `C:\Tools\NetworkProfileManager\`).
3. `NetworkProfileManager.exe`'ye çift tıkla. UAC onayı sonrası açılır.

> SmartScreen "Bilinmeyen yayıncı" uyarısı çıkarsa → **Daha fazla bilgi → Yine de çalıştır.**

### Güncelleme

Yeni sürüm için Releases sayfasını kontrol edin. Yeni zip'i indirip eski klasörün üstüne çıkarın — profiller `%LOCALAPPDATA%\NetworkProfileManager\data\` altında, etkilenmez.

### Kaynak koddan derleme

```powershell
git clone https://github.com/rmzncakir/network-profile-changer.git
cd network-profile-changer
dotnet build -c Release
dotnet publish NetworkProfileManager/NetworkProfileManager.csproj `
    -c Release -r win-x64 --self-contained true -o publish
```

Çıktı `publish\` klasöründe.

---

## Veri konumu

Profiller, geçmiş ve tema ayarları:

```
%LOCALAPPDATA%\NetworkProfileManager\data\
├── profiles_<adapter-id>.json
├── history_<adapter-id>.json
└── app_settings.json
```

Kaldırma sırasında bu klasörü manuel silebilirsin.

---

## Güvenlik

- **netsh argümanları** `ProcessStartInfo.ArgumentList` ile geçilir — string interpolation yok, komut enjeksiyonu engellenir.
- **IPv4 validasyonu** canonical dotted-quad formatı gerektirir (`192.168.1` gibi shorthand'ı reddeder — silent misconfig koruması).
- **Adaptör adı sanitization** control char, quote, CR/LF ve null byte'ı reddeder.
- **Profil dosya adı sanitization** path traversal'ı (`../../etc/passwd` vb.) engeller.
- Uygulama tek bir UAC promptu ile yönetici olarak başlar; per-action elevation döngüsü yoktur.

Zafiyet bildirimi için [SECURITY.md](SECURITY.md).

---

## Testler

```powershell
dotnet test NetworkProfileManager.Tests/NetworkProfileManager.Tests.csproj
```

xUnit ile **80+ test** — NetworkService validation, repository roundtrip + path-traversal koruması, IDataErrorInfo flow, RelayCommand, AppPaths, IpScannerService.

---

## Mimari

```
NetworkProfileManager/
├── Models/         IpProfile, AdapterInfo, HistoryEntry, ScanResult
├── Services/       NetworkService (netsh), Profile/HistoryRepository, IpScannerService, NetworkMonitorService
├── ViewModels/     MainViewModel, AdapterViewModel, ProfileViewModel, IpScannerViewModel
├── Views/          MainWindow, AdapterListPanel, ProfilePanel, HistoryPanel, AddProfileDialog,
│                   IpScannerWindow, PingWindow, ThemeSettingsWindow, TrayPopupWindow
├── Themes/         Dark.xaml, DarkBlue.xaml, Light.xaml
├── Strings/        Tr.xaml, En.xaml — UI dizinleri (DynamicResource ile bağlanır)
├── Helpers/        AppPaths, NotificationService, StartupHelper, LanguageManager, Loc
└── Converters/     BoolToVisibility, StatusToColor, AdapterTypeToSymbol, ...
```

MVVM pattern. UI bağımsız sınıflar `NetworkProfileManager.Tests` ile kapsanır.

---

## Katkı

PR'lar açık. Lütfen:

1. Yeni kod test ile gelsin (`NetworkProfileManager.Tests/`).
2. `dotnet build` ve `dotnet test` geçtiğinden emin ol.
3. Güvenlik etkili bir değişiklikse açıkla.

---

## Lisans

[MIT](LICENSE) © rmzncakir
