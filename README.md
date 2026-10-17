# Universal Bridge

<img src="docs/images/app-icon.png" width="96" height="96" alt="Universal Bridge icon">

**[English](README.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)**

Share a Windows PC's keyboard, mouse, and supported trackpad gestures with an iPad over Bluetooth. A native Windows 11 app built with C# / .NET 10 / WPF, released under the [MIT license](LICENSE).

No iPad companion app, extra hardware, or internet connection is needed during use. The PC's Bluetooth adapter and driver must support the **BLE Peripheral** role.

**Preview v0.3.1.** Basic keyboard and mouse input has been confirmed by a user on iPad. Gesture mappings, app-specific shortcuts, and different hardware still need real-device testing. This project is independent of Apple and Microsoft.

![Universal Bridge dashboard](docs/images/dashboard-en.png)

## Download

Open **[Releases — v0.3.1](https://github.com/noppy3325-droid/universal-bridge/releases/tag/v0.3.1)**:

- **[UniversalBridge-0.3.1-win-x64.zip](https://github.com/noppy3325-droid/universal-bridge/releases/download/v0.3.1/UniversalBridge-0.3.1-win-x64.zip)** — extract and run `UniversalControl.exe`. Includes .NET; no separate runtime installation.
- `UniversalBridge-0.3.1-source.zip` — source without SDKs, caches, settings, or logs.
- `SHA256SUMS.txt` — SHA-256 checksums for the executable and archives.

Windows x64 only. The executable is unsigned; no installer, MSIX, or code signing is provided.

## Features

| Feature | What it does |
|---|---|
| Bluetooth keyboard/mouse | Sends standard HID reports to one selected, paired iPad, with encrypted GATT access. |
| Screen-edge switching | Configure the display, edge, switching area, and hold time; respects adjacent Windows monitors. |
| Editable shortcuts | **Ctrl+Alt+I** to iPad; **Ctrl+Alt+Q** to Windows by default. Edit both in Input settings. **Esc three times** always provides emergency return. |
| Precise relative mouse input | Raw Input avoids Windows acceleration; sensitivity **0.02–4.00** in 0.01 steps, preserving fractional movements. |
| Trackpad mappings | Two-finger vertical/horizontal scrolling with dedicated **0.25–12.00 ×** sensitivity (default **3.00 ×**), pinch-to-zoom shortcuts, and configurable three-to-five-finger swipes. |
| Three UI languages | Japanese, English, Simplified Chinese, or system language; applies immediately and persists. |
| Desktop integration | Custom application/tray icon, light/dark theme, optional sign-in startup, diagnostics, and bounded reconnect attempts. |

**Gesture mappings do not forward native iPad multitouch contacts or Magic Trackpad's protocol.** They generate scroll reports and supported keyboard shortcuts. Zoom, Home, and other actions depend on iPadOS and the app.

## Quick start

1. Exit older versions via **Settings & diagnostics → Exit application**. Extract the ZIP and launch `UniversalControl.exe` as a normal user.
2. Select **日本語 / English / 简体中文 / System language** at the top right.
3. Choose **Start Bluetooth HID**. Unsupported hardware or policy restrictions appear in the app.
4. In iPad **Settings → Bluetooth**, select the **Windows PC's computer name**. Confirm pairing on both devices yourself.
5. Select the iPad in the app. Pairing and both keyboard/mouse subscriptions must be ready.
6. Press **Ctrl+Alt+I and release all keys**, or use the switch button. Test typing, clicks, and scrolling in Notes. Return with **Ctrl+Alt+Q** or **Esc three times**.
7. Adjust sensitivity, gesture assignments, and shortcuts in **Input settings**. Shortcuts require **Apply shortcuts**; other changes save automatically. Enable edge switching after testing input.

During gesture capture, a small foreground window receives touchpad input. Focusing another Windows window returns control to Windows. Capture needs compatible **experimental Precision Touchpad APIs on Windows 11 24H2 or later**, checked at runtime. If unavailable, turn capture off to share keyboard/mouse only; Windows system gestures are then not blocked.

Upgrading from 0.1.x changes the mouse report from 6 to 7 bytes for horizontal scrolling. If mouse input stops, reconnect; if necessary, manually forget the PC on iPad and pair again.

In v0.3.1, **Input settings → Two-finger scroll sensitivity** adjusts captured touchpad scrolling independently of the mouse wheel. The recommended 3.00 is three times the previous default. Numeric input, 0.25 steps, and a reset button are available. Cumulative motion preserves movement at gesture onset and completion. Standard HID still uses whole scroll ticks rather than pixel scrolling; no extra momentum is simulated.

## Privacy and recovery

Input text and HID payloads are not logged. Settings/connection diagnostics stay in `%LOCALAPPDATA%\UniversalBridge`; logs rotate at 1 MiB. No telemetry or runtime cloud service is used.

Connection, subscriptions, input hooks, and released keys are checked before switching. Disconnects, capture focus loss, queue failure, sleep, and session changes return control to Windows. Reconnecting never captures input automatically. Sudden termination cannot send release reports to iPad; recovery there depends on Bluetooth disconnect handling.

To uninstall, disable optional startup, exit, and remove the extracted folder. Remove local settings and iPad pairing separately if desired.

## Build and test

Windows 11 x64, PowerShell 7, **.NET SDK 10.0.402**. Restore needs internet; runtime does not. Minimum OS build: 22000; gesture capture additionally needs supported APIs on build 26100 or later.

```powershell
# Optional: Microsoft's verified SDK, local to this folder.
pwsh -File scripts/Setup-Sdk.ps1
# Build, tests, and self-contained Windows package.
pwsh -File scripts/Build.ps1 -Test -Publish
# Binary/source archives and checksums.
pwsh -File scripts/Package.ps1
```

SDK/packages stay under `.tools/`. Windows SDK projection: `10.0.26100.87`; experimental APIs are isolated and checked. No drivers, registry workarounds, VMs, or OS security changes are installed.

**120 automated tests** cover HID, ownership/recovery, motion precision, shortcuts, catalog completeness, format placeholders, and language persistence. GUI smoke tests exercise three languages without capturing input. These do not replace iPad hardware tests.

See [contributing/translations](CONTRIBUTING.md), [architecture](ARCHITECTURE.md), [limitations](KNOWN_LIMITATIONS.md), [verification](TEST_REPORT.md), and [hardware checks](docs/MANUAL_TESTS.md). Detailed engineering notes are currently Japanese. GitHub Actions tests pushes/PRs; version tags build downloadable preview releases.

## License and credits

[MIT](LICENSE). HID descriptor/keymap portions adapted from [windows-ble-hid](https://github.com/abhishek-raj/windows-ble-hid); OpenSpan reviewed as research. Attribution and runtime notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [licenses](licenses).
