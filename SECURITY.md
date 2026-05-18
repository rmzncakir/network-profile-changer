# Güvenlik Politikası / Security Policy

## Desteklenen Sürümler

En son `main` branch'i ve son release destek alır. Eski release'lere geriye dönük yama yapılmaz.

## Zafiyet Bildirimi / Reporting a Vulnerability

Lütfen güvenlik zafiyetlerini **public issue açmadan** bildirin.

GitHub'ın **Private Vulnerability Reporting** özelliğini kullanın:
👉 [Report a vulnerability](../../security/advisories/new)

İçerik:
- Etkilenen sürüm
- Reprodüksiyon adımları
- Etki / saldırgan modeli
- (Varsa) önerilen düzeltme

Yamanın yayına alınmasına kadar açıklamayı koordineli tutmanızı rica ederiz.

## Tehdit Modeli (Threat Model)

Uygulama **yerel Windows kullanıcısı** içindir ve yönetici hakkıyla çalışır. Saldırı yüzeyleri:

| Vektör | Durum |
|---|---|
| `netsh` argument injection (adapter name / IP) | ✅ Kapatıldı — `ProcessStartInfo.ArgumentList` + sıkı validation |
| Profile/history JSON path traversal | ✅ Kapatıldı — `Regex.Replace(id, @"[^\w\-]", "_")` |
| Malformed IP silent misconfig (`192.168.1` → 192.168.0.1) | ✅ Kapatıldı — canonical dotted-quad zorunlu |
| Newtonsoft.Json TypeNameHandling RCE | ✅ Yok — default `None` |
| Self-elevation `--apply <base64>` parsing | ✅ Kaldırıldı — manifest zaten admin, IPC döngüsü yok |
| Lokal kullanıcı manuel profil JSON manipülasyonu | ⚠️ Out-of-scope — admin hakkı varsa kullanıcı zaten netsh çalıştırabilir |
