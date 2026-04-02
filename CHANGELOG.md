# BoatTron Monitor — Windows Client Changelog

---

## [2.23] - 2026-04-02

### Fixed — WiFi AP detection: active hardware scan via WlanScan()
- `WifiManager`: replaced `netsh`-only trigger with P/Invoke to `wlanapi.dll`
- `WlanScan()` commands the adapter to perform an immediate channel sweep —
  `netsh wlan show networks` alone only reads the stale adapter cache
- AP now appears in the first poll after the 2-second sweep completes

---

## [2.22] - 2026-04-02

### Fixed — Discarded first netsh result
- Removed warmup call that called `netsh wlan show networks` and threw the result away
- If the AP was already in cache it was being discarded before polling began

---

## [2.21] - 2026-04-02

### Added — Indefinite WiFi scan with Stop Scanning button
- WiFi scan now runs until the AP is found or user clicks Stop
- "Scan Now" button text toggles to "Stop Scanning" during active scan
- Each poll that finds no new networks posts `(no new networks found)` to the scroll

---

## [2.20] - 2026-04-02

### Fixed — Password dialog clipping
- Form taller (150px client height, was 110px)
- Label height 36px with 10pt font — no longer overlaps the text box
- OK / Cancel buttons 84×32 (was 72×26) — fully within bounds

---

## [2.19] - 2026-04-02

### Changed — Smooth scroll during WiFi scan
- Networks scroll at 1 second per entry (queue + background consumer)
  instead of firing instantly as a burst
- When a BoatTron AP is found: non-BoatTron queue cleared, AP shown immediately,
  scan stops automatically
- All found SSIDs logged individually for diagnostics

---

## [2.18] - 2026-04-02

### Changed — Main window layout overhaul
- Removed Settings button from toolbar; Settings accessible via right-click context menu
- 6-line scrolling status panel below button bar:
  - Line 0: bold current status
  - Lines 1–5: sub-status history, newest at bottom, scrolls up
- Form: 1000×680 default, 860×540 minimum
- Uninstall prompt now fires in `InitializeSetup` (before wizard opens),
  not `PrepareToInstall` (after user clicks Install)

### Fixed — Installer: old-version registry detection
- Pascal `const AppGuidKey` with literal GUID string eliminates double-brace
  preprocessor expansion bug (`'{#MyAppId}_is1'` produced `'{{GUID}_is1'`)

---

## [2.17] - 2026-04-01

### Changed — ConfigureForm redesign
- Taller dialog (520×430, was 500×320); row spacing 44px (was 32px)
- "AP password" → "Hotspot password" throughout
- Hint: "(leave blank to keep the current password)"
- UDP port field removed — no longer configurable from this dialog
- Title: `Configure — BoatTron-XXXX`

### Changed — SettingsForm redesign
- UDP port field removed
- Explanation label added describing AP hotspot password
- Compact size: 420×170

---

## [2.6] - 2026-03-30

### Added — Initial release
- WinForms .NET 8 single-file self-contained executable
- UDP broadcast LAN discovery; auto-refresh every 10 minutes
- Device cards with Update / Configure / Alarms buttons
- ConfigureForm: AP mode / Network mode tabs; WiFi SSID dropdown; reboot
- AlarmsForm: up to 4 alarms per device; set/reset/preview
- SettingsForm: AP password (SHA-256 hashed); UDP port
- WiFi AP switching via `netsh` — connects to BoatTron AP, restores original network
- Inno Setup 6 installer: uninstall detection, desktop/Start Menu shortcuts
- GitHub Actions workflow: builds installer on every push, commits back to repo
