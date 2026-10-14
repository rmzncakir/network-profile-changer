# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] — 2026-10-14

### Added

- **Multi-method network discovery.** The IP scanner no longer relies on ICMP alone.
  A host is reported as present if *any* probe succeeds:
  - **ARP** (`SendARP`) — authoritative on the local subnet and answered beneath the
    firewall, so it finds devices that silently drop ping.
  - **ICMP** — overlapped with the ARP call, and the only source of a real latency figure.
  - **TCP connect** — reaches hosts off the local subnet and reveals open ports.

  On a live /24, ICMP alone found 24 hosts; the new pipeline found 28, in 6.2 s,
  with a MAC address for every host.
- **Device type hints** from open ports (Modbus, S7/ISO-TSAP, EtherNet/IP, OPC UA,
  RTSP, JetDirect, SMB/RDP, SSH, HTTP). Reported as *protocol (port)* rather than a
  device verdict, because a single open port is weak evidence.
- **"Found by" and "open ports" columns** in the results grid and CSV export.
- **Monitoring mode** — re-scans on an interval and notifies when a device appears
  that was not in the previous sweep. Devices are keyed by MAC so a DHCP lease
  change is not mistaken for a new device.
- **Vendor presets** — one click fills the edit bar with the laptop-side address for
  a vendor's usual subnet (Siemens, Allen-Bradley, Mitsubishi, Beckhoff, camera, DHCP).
- **Profile import / export** as JSON, with imported entries validated exactly like
  hand-typed ones.
- **Ctrl+1 … Ctrl+5** apply the selected adapter's saved profiles.
- **Per-apply UAC elevation.** The app runs as `asInvoker` (so it can auto-start from
  the `Run` key, which Windows skips for `requireAdministrator` apps) and elevates a
  hidden worker for the netsh batch — one prompt per apply, not per command.
- **Confirmation dialogs** for deleting a profile and clearing history.
- **Tray hint** the first time the window is closed to the tray.
- **SHA256 checksums** on every release, plus dormant VirusTotal and SignPath steps
  that activate once their secrets are configured.

### Changed

- **Windows 11 look.** The custom navy/neon palette is gone; every window now uses
  the Fluent theme resources, the Mica backdrop and the Windows accent colour.
  Theme choices are *System* (default, follows Windows light/dark), *Light* and
  *Dark*; the old *Dark Blue* setting maps to *Dark*. On Windows 10 the windows fall
  back to a solid background.
- **Main window reorganised** around the selected adapter: its live IP, mask,
  gateway, DNS and DHCP/static state at the top, then profiles, ping and history.
  Manual configuration is pinned below them so it never scrolls away, and vendor
  presets are now one-click buttons there instead of a drop-down menu. The adapter
  list is a plain navigation list; panel header strips and the status bar are gone
  (messages go to the notification bar).
- Profiles are full-width rows showing a one-line summary (`192.168.0.100/24 · GW …`)
  and their Ctrl+1..5 shortcut. The active profile has an accent check before its
  name; edit and delete moved into a "…" menu.
- A profile counts as **active** only when the adapter's address, mask and gateway
  all match it (and DNS, when both sides report one). Previously a matching IP was
  enough, so a profile with a different gateway looked active.
- Dialogs (settings, add/edit profile, ping, scanner) use labelled fields and a
  footer action bar; settings use drop-downs instead of radio lists.
- Scanner: start and stop share one button slot, and the status column is localised.
- Scanner defaults favour completeness: timeout 500 ms → 1000 ms, parallelism 50 → 128.
- ARP resolution now uses the `SendARP` API instead of spawning `arp.exe` per host.
- The sweep runs on dedicated threads; blocking ARP calls on pool threads tripped the
  thread pool's slow-injection heuristic and serialized the scan.
- "Enable at startup" rewrites the `Run` entry from the running executable's path
  instead of leaving a stale one behind.

### Fixed

- **Copy buttons could crash the app** when another program (RDP clipboard sync, a
  clipboard manager) held the clipboard; they now report "could not copy" instead.
- **Wrong adapter could be configured.** Apply / apply-profile / revert re-read the
  selected adapter *inside* the background task, so changing the selection mid-apply
  could send the address to a different adapter or record the wrong history entry.
- **`netsh` timeout was ignored**, so a hung call surfaced as an opaque
  `InvalidOperationException` and left the process orphaned.
- **`NetworkMonitorService` never ran** despite being documented as a feature.
- **CSV formula injection** — hostnames come from attacker-influenceable reverse DNS
  and were written to CSV unescaped.
- **CSV export could crash the app** when the target file was unwritable.
- Scanner settings were written next to the executable, so they were silently lost
  under an ACL-protected install location.
- Unbounded scan ranges could exhaust memory; range and parallelism are now clamped.
- Light theme leaks: the busy overlay, ping colours and status dots were pinned to the
  dark palette.
- Turkish strings leaked into the English UI in the scanner.
- Accessibility: icon-only buttons had no accessible name, and dialogs ignored Esc.

### Security

- The elevated worker accepts only `interface ip set address`, `set dns` and `add dns`,
  rejecting every other command and any argument containing control characters or
  quotes. Covered by tests.

## [1.0.0] — 2026-05-18

Initial public release: adapter list, static/DHCP profiles, history with revert,
IP scanner, ping tool, system tray, Turkish/English UI, light/dark/blue themes.

[Unreleased]: https://github.com/rmzncakir/network-profile-changer/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/rmzncakir/network-profile-changer/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/rmzncakir/network-profile-changer/releases/tag/v1.0.0
