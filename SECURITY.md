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

The app runs as the signed-in user (`asInvoker`). Applying a change starts a hidden, elevated copy of the
same executable through UAC; it runs one batch of `netsh` commands from a request file and exits.
Mitigations in place:

| Vector | Status |
|---|---|
| `netsh` argument injection (adapter name / IP) | ✅ Mitigated — `ProcessStartInfo.ArgumentList` + strict input validation |
| Profile/history JSON path traversal | ✅ Mitigated — `Regex.Replace(id, @"[^\w\-]", "_")` on filenames |
| Malformed IP silent misconfig (`192.168.1` → 192.168.0.1) | ✅ Mitigated — canonical dotted-quad required |
| Newtonsoft.Json `TypeNameHandling` RCE | ✅ Not applicable — default `None` is used |
| Elevated worker (`--run-netsh`) used to run other commands | ✅ Mitigated — only `interface ip set address`, `set dns` and `add dns` are accepted; arguments with control characters or quotes are refused (unit-tested) |
| Request file replaced between writing and the UAC prompt | ⚠️ Limited — the file sits in the user's temp folder, so a process already running as that user could swap it; the whitelist above caps the effect at changing an adapter's IP / DNS |
| DLL planted beside the executable (loaded when elevated) | ⚠️ Not mitigated in code — extract the zip to a folder only administrators can write to, e.g. under `C:\Program Files\` |
| CSV formula injection via reverse-DNS hostnames | ✅ Mitigated — fields starting with `= + - @` are escaped on export |
| Imported profile JSON | ✅ Validated exactly like typed input; invalid entries are skipped |
| Local user manually editing profile JSON | ⚠️ Out of scope — the same user can already change their own adapter settings; values are still validated before use |
