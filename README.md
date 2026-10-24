# Universal Bridge

<img src="docs/images/app-icon.png" width="96" height="96" alt="Universal Bridge icon">

**[English](README.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)**

**Windows on your desk. iPad beside it. One keyboard and mouse.**

Share a Windows PC's keyboard, mouse, and supported trackpad gestures with an iPad over Bluetooth. A native Windows 11 app built with C# / .NET 10 / WPF, released under the [MIT license](LICENSE).

No iPad companion app, extra hardware, or internet connection is needed during use. The PC's Bluetooth adapter and driver must support the **BLE Peripheral** role.

**Preview v0.5.1.** Basic keyboard and mouse input has been confirmed by a user on iPad. Gesture mappings, app-specific shortcuts, and different hardware still need real-device testing. This project is independent of Apple and Microsoft.

**[Download the preview](https://github.com/noppy3325-droid/universal-bridge/releases/tag/v0.5.1) · [Check requirements](#before-you-download) · [Quick start](#quick-start)**

![Universal Bridge dashboard](docs/images/dashboard-en.png)

## Who is it for?

- **Windows + iPad desks:** keep working on your PC, then switch the same keyboard and mouse to an iPad for a search or a note.
- **Laptop users with limited desk space:** try sharing the controls you already have, without a second keyboard or a VM on supported PCs.
- **Early adopters and contributors:** explore a native C# / WPF BLE HID implementation with Japanese, English, and Simplified Chinese UI.

Apple's [Universal Control requires a Mac](https://support.apple.com/en-us/102459). Universal Bridge focuses on Windows-to-iPad input sharing. It does not provide screen mirroring, shared clipboard, or file transfer. Contact forwarding is an experimental opt-in mode described below.

## Before you download

You need **Windows 11 x64**, an iPad, and a PC Bluetooth adapter **and driver supporting BLE Peripheral**. Bluetooth support alone does not establish compatibility; the app checks the adapter and reports unsupported configurations. Gesture capture additionally requires supported experimental APIs on Windows 11 24H2 or later. Keyboard/mouse sharing can be used with gesture capture off.

This is an **unsigned preview** with limited real-device coverage. If your hardware is unsupported, no purchase is needed to report the result. Optional [compatibility reports](https://github.com/noppy3325-droid/universal-bridge/issues/new?template=compatibility.yml) help identify which setups work.

## Download

Open **[Releases — v0.5.1](https://github.com/noppy3325-droid/universal-bridge/releases/tag/v0.5.1)**:

- **[UniversalBridge-0.5.1-win-x64.zip](https://github.com/noppy3325-droid/universal-bridge/releases/download/v0.5.1/UniversalBridge-0.5.1-win-x64.zip)** — extract and run `UniversalControl.exe`. Includes .NET; no separate runtime installation.
- `UniversalBridge-0.5.1-source.zip` — source without SDKs, caches, settings, or logs.
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

**The default compatibility mode** generates scroll reports and supported keyboard shortcuts. Actual-contact relay requires the experimental opt-in mode below. Magic Trackpad's proprietary protocol is not used. Zoom, Home, and other actions depend on iPadOS and the app.

## 0.5.0: iPad action shortcuts and edge switching

Dedicated actions work only while controlling iPad. Edit all five bindings together in **Input settings → Shortcuts**, then choose **Apply shortcuts**. Duplicate bindings are rejected. The three action chords remain ordinary keys while controlling Windows.

| iPad action | Default | Sent keys |
|---|---|---|
| Home | **Ctrl+Alt+H** | ⌘H, on compatible iPadOS/settings |
| Search | **Ctrl+Alt+S** | ⌘Space |
| Next app | **Ctrl+Alt+N** | ⌘Tab |

Holding a trigger runs the action once, without typing H/S/N. Release other character keys and mouse buttons first. The action shortcuts can be disabled independently. Home uses the compatibility ⌘H path; it does not emulate Apple's Globe key. **The new actions have not been verified on iPad hardware.** Apple's current iPadOS guidance uses Globe+H, so Home support depends on iPadOS and settings.

**To switch at the edge:** start HID, select the connected iPad, then enable edge switching in **Display layout**. Select a Windows monitor and direction, or drag the iPad diagram above, below, left or right. Blue lines show the available portal. Move the cursor to that edge and wait for the configured delay: 300ms by default, adjustable from 150 to 1500ms. The wait completes even after mouse movement stops.

Shared Windows-monitor boundaries are excluded; partial adjacency leaves the uncovered segment usable. Leaving the edge, pressing a key/button or changing settings cancels a pending activation. Remaining at the same edge cannot repeatedly reactivate it. The UI explains off, HID waiting, missing target and readiness states. Edge switching is off by default. **The iPad's actual pointer coordinates cannot be read, so control resumes at its previous position there.** Return to Windows with Ctrl+Alt+Q or Esc three times.

[Settings screenshots — shortcuts](docs/images/shortcuts-en.png) · [Settings screenshots — layout](docs/images/layout-en.png)

## 0.4.0: fast movement and experimental native gestures

- **Stabilize fast pointer movement (on by default):** a directional soft limit reduces sudden fast movement without replaying the compressed distance later. Fast travel is intentionally shorter. Turn this off in Input settings for the previous linear transfer. Adjacent queued motion is now merged only up to 127 counts per axis to avoid oversized bursts.
- **Two-finger scroll onset:** compatibility mode uses nearest-tick rounding with fractional carry for earlier response. Dedicated scroll sensitivity remains available. It still sends integer wheel ticks rather than pixel scrolling.
- **Contact relay for native iPad gestures (experimental, off by default):** forwards actual two-to-five-finger contacts from a supported Precision Touchpad as standard HID Touch Pad reports, leaving scroll, pinch and swipe recognition to iPadOS. **iPad recognition and behavior have not been verified on hardware. Full Magic Trackpad parity is not promised.** Windows generally does not expose contact streams for taps/holds, so all native gestures cannot be reproduced. One-finger pointing keeps the mouse path.

To try it, enable **Input settings → Contact relay for native iPad gestures**, then **fully exit and restart the app**. Start HID and check the contact subscription indicator. The Report Map changes: if iPad does not recognize it, manually forget the PC on iPad and pair again. The app never removes pairing itself. Restart when disabling it too. The mode targets the standard iPadOS 14.5+ trackpad specification and requires compatible Windows APIs, all three input subscriptions, Surface Switch and notification capacity of at least 28 bytes. Switching is refused with a reason when these are missing.

Native mode bypasses the app's scroll gain and pinch/swipe key mappings; iPad handles interpretation. Diagnostics record API support, frame counts and maximum contact counts, without coordinates or input payloads. See [hardware checks](docs/MANUAL_TESTS.md).

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

Since v0.3.1, **Input settings → Two-finger scroll sensitivity** adjusts captured touchpad scrolling independently of the mouse wheel. The recommended 3.00 is three times the previous default. Numeric input, 0.25 steps, and a reset button are available. Cumulative motion preserves movement at gesture onset and completion. Standard HID still uses whole scroll ticks rather than pixel scrolling; no extra momentum is simulated.

## Privacy and recovery

v0.3.4 introduced improved fine diagonal motion with centered rounding and fractional carry, keeps direction when splitting large movement, and uses a private high-resolution mouse report timer. The local cadence supports 8 ms when the BLE link allows it; the actual negotiated connection interval still applies. Adjacent unsent XY movement can coalesce under load, with keys, clicks, wheel/pan, reversal, and range limits kept as barriers. Existing sensitivity settings are retained. iPad cursor feel and wireless latency still need real-device verification.

v0.3.3 fixes a reproducible case where the iPad remained connected but switching was refused as “input held.” A JIS half/full key's IME mode value could remain in the local held-key check after returning to Windows. The physical OEM key is now checked instead, and released quarantine entries are cleaned up. Rejection diagnostics record only key counts and mouse/idle flags, without key identities or typed content. Other intermittent failures still need their own diagnostic evidence.

After iPad sleep/wake, unlock the iPad and wait for both report subscriptions, then press your iPad activation shortcut again. Since v0.3.2 the app tracks replacement GATT sessions and recovers a failed sender after successfully releasing both keyboard and mouse. Input is never recaptured automatically; an unfinished timed-out send must complete before recovery can proceed. If the iPad remains disconnected, reconnect the PC from iPad Settings → Bluetooth.

On a Windows Japanese keyboard, **半角/全角** (also Alt+半角/全角) now sends one **Control+Space** tap to cycle iPad input languages instead of typing punctuation. Holding the key does not repeatedly cycle languages. US backtick keeps its usual mapping. Add both Japanese and English in iPad Settings → General → Keyboard → Keyboards; language switching follows [iPad's external-keyboard shortcut](https://support.apple.com/en-euro/guide/ipad/ipaddd28d7ed/ipados). Keyboard layout/IME behavior still depends on iPadOS settings.

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

**192 automated tests** cover HID, diagonal precision and splitting, motion backpressure/order, sleep/reconnect recovery, JIS input, shortcuts, translations, and language persistence. GUI smoke tests exercise three languages without capturing input. These do not replace iPad hardware tests.

See [contributing/translations](CONTRIBUTING.md), [architecture](ARCHITECTURE.md), [limitations](KNOWN_LIMITATIONS.md), [verification](TEST_REPORT.md), and [hardware checks](docs/MANUAL_TESTS.md). Detailed engineering notes are currently Japanese. GitHub Actions tests pushes/PRs; version tags build downloadable preview releases.

## License and credits

[MIT](LICENSE). HID descriptor/keymap portions adapted from [windows-ble-hid](https://github.com/abhishek-raj/windows-ble-hid); OpenSpan reviewed as research. Attribution and runtime notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [licenses](licenses).
