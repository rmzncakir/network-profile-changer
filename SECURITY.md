# Security Policy

## Supported Versions

Only the latest `main` branch and the most recent release receive fixes. Older releases are not patched.

## Reporting a Vulnerability

Please **do not open public issues** for security vulnerabilities.

Use GitHub's **Private Vulnerability Reporting** feature instead:
👉 [Report a vulnerability](../../security/advisories/new)

Please include:

- Affected version
- Reproduction steps
- Impact / attacker model
- (Optional) suggested fix

We'll keep disclosure coordinated until a patch is published.

## Threat Model

The app runs **locally as administrator** on the user's Windows machine. Mitigations in place:

| Vector | Status |
|---|---|
| `netsh` argument injection (adapter name / IP) | ✅ Mitigated — `ProcessStartInfo.ArgumentList` + strict input validation |
| Profile/history JSON path traversal | ✅ Mitigated — `Regex.Replace(id, @"[^\w\-]", "_")` on filenames |
| Malformed IP silent misconfig (`192.168.1` → 192.168.0.1) | ✅ Mitigated — canonical dotted-quad required |
| Newtonsoft.Json `TypeNameHandling` RCE | ✅ Not applicable — default `None` is used |
| Self-elevation `--apply <base64>` parsing | ✅ Removed — manifest already runs as admin; no IPC loop |
| Local user manually editing profile JSON | ⚠️ Out of scope — an admin-capable user can already run netsh directly |
