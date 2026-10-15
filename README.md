# PocketMic LAN

[![Version](https://img.shields.io/badge/version-v0.1.7-b88a3b)](https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.7)
[![CI](https://github.com/ryanspice/pocketmic-lan/actions/workflows/ci.yml/badge.svg)](https://github.com/ryanspice/pocketmic-lan/actions/workflows/ci.yml)
[![iOS CI](https://github.com/ryanspice/pocketmic-lan/actions/workflows/ios.yml/badge.svg)](https://github.com/ryanspice/pocketmic-lan/actions/workflows/ios.yml)
[![macOS CI](https://github.com/ryanspice/pocketmic-lan/actions/workflows/macos.yml/badge.svg)](https://github.com/ryanspice/pocketmic-lan/actions/workflows/macos.yml)
[![Lighthouse](https://github.com/ryanspice/pocketmic-lan/actions/workflows/lighthouse.yml/badge.svg)](https://github.com/ryanspice/pocketmic-lan/actions/workflows/lighthouse.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

PocketMic turns a phone into an encrypted microphone for a desktop application on the same private LAN. Android sends 48 kHz mono audio over UDP with AES-256-GCM; no PocketMic account or cloud relay is required. Windows receives PCM or Opus and can route playback through a separately installed virtual cable.

## Release and downloads

**[0.1.7 is a published development prerelease](https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.7).** It includes the WinUI 3 Mica receiver, classic fallback, persistent tray and single-instance activation, Android-derived desktop icons, connection fixes and a complete Windows package with native Opus. The earlier source baseline is preserved as `v0.1.6`.

- [Android APK — debug-signed](https://github.com/ryanspice/pocketmic-lan/releases/download/v0.1.7/app-debug.apk)
- [Windows x64 receiver and per-user installer — unsigned](https://github.com/ryanspice/pocketmic-lan/releases/download/v0.1.7/PocketMicReceiver-win-x64.zip)
- [Release checksums](https://github.com/ryanspice/pocketmic-lan/releases/download/v0.1.7/SHA256SUMS.txt)
- [Release evidence and remaining acceptance gates](docs/releases/0.1.7/release.md)
- [Short technical walkthrough: Mica, desktop polish and Apple builds](https://blog.ryanspice.com/2026/10/15/pocketmic-lan-1-7-mica-and-apple-builds/)

Android/Windows is the established development-release path. The iOS sender and macOS receiver/virtual microphone are **unsigned development previews**, with simulator, protocol and packaging checks in [GitHub Actions](https://github.com/ryanspice/pocketmic-lan/actions). Apple currently uses PCM v1 and manual pairing; physical-device acceptance and distribution decisions are in the [0.1.8 plan](docs/plans/0.1.8-apple.md). These are not App Store, TestFlight or notarized installers.

## Install and connect

1. Extract the complete Windows ZIP and run `Install-PocketMic.cmd`. It creates Start Menu/Desktop shortcuts, preserves settings and retains the previous installation. For portable use, run `PocketMicReceiver.exe --show` from the extracted folder.
2. Install the Android APK. `com.canopydigital.pocketmic` installs separately from v0.1.5's `com.ryanspice.pocketmic`; stop the old stream and pair the new app because settings are not migrated. Updates require the same application ID and signing certificate; independently built debug APKs may use different certificates.
3. Open the receiver and select speakers for an initial audio check. It listens automatically by default; an existing preference to disable auto-listen is preserved.
4. On Android, use **Available PCs** or scan the receiver's QR code. Choose the PC address on the phone's LAN if it has multiple network interfaces. External pairing links require confirmation before replacing settings; manual IPv4/port/key entry remains available.
5. Tap **Start** on the phone. Confirm audio at the selected destination. Readiness and packet delivery indicators alone do not establish audible playback.

On Windows 11 22H2 and newer the app uses a full Mica canvas and native Fluent controls. Older Windows opens the classic interface; `--classic --show` selects it explicitly. Closing the window leaves PocketMic in the tray; opening it again restores the same receiver. Choose tray **Exit** to stop it. See the [Windows package guide](windows-receiver/PACKAGE-README.md) for details.

### Route into Discord, Teams, OBS or a game

Install [VB-Audio VB-CABLE](https://vb-audio.com/Cable/) separately, select **CABLE Input** as PocketMic's playback device, then select **CABLE Output** as the target app's microphone. The receiver's Windows-microphone control changes recording defaults for an available matching cable; PocketMic does not install its own Windows microphone driver.

### Discovery and firewall

Keep both devices on the same trusted LAN. Guest-network isolation, VPN/network selection and firewall rules can affect discovery. Start the receiver, use **Refresh PCs**, verify the key, and use manual entry if needed. Wrong keys prevent authenticated audio even when a PC is listed.

Allow the selected audio UDP port, its control port (`audioPort + 1`) and discovery port `49501` on **Private networks**. Default audio port: `49500`. Custom-port receivers also answer standard discovery probes.

```powershell
.\scripts\allow-firewall.ps1
.\scripts\allow-firewall.ps1 -Port 49520
```

Do not port-forward the receiver. Pairing keys are protected at rest on Android, but Windows stores its key in the user's settings file. This is private-LAN voice software, not a hardened public-internet service or a measured real-time music-monitoring system.

## Build and contribute

Start with [development and packaging](docs/development.md), [testing](docs/testing.md) and [repository instructions](AGENTS.md). The Android helper uses the committed Gradle wrapper and normally runs lint/tests. The Windows publisher builds modern/classic packages but does not run tests or supply a native Opus DLL automatically; use its documented `-OpusDllPath` input for a complete local package.

```powershell
python tools/verify_docs.py
python scripts/verify_release_versions.py
python tools/verify_protocol.py
python tools/verify_receiver_logic.py
```

See [the documentation map](docs/README.md) for the [protocol](docs/protocol.md), [Windows UI](docs/windows-ui.md), [product intent](INTENT.md), [roadmap](docs/plans/roadmap.md), [Mac tester setup](macos/README.md) and [localization status](localization/README.md). Sustained Opus audio, destination-app routing, measured latency and broader hardware/accessibility acceptance remain separate from automated build/test results.

## Repository map

```text
android/                 Kotlin/Compose transmitter and native encoder
windows-receiver/        Launcher, classic UI and package documents
windows-receiver-winui/  Modern WinUI 3 interface and Mica canvas
windows-receiver-core/   Shared engine, settings, routing and protocol
windows-receiver-tests/  Windows regression and native codec tests
ios/  macos/             Apple preview apps and Mac virtual microphone
localization/            Catalogs, status and validators
scripts/  tools/  tests/ Build/package helpers, checks and protocol fixtures
dev/v3/                  Canonical website candidate used by CI/Lighthouse
web/                     Legacy demo and historical screenshot assets
web-v2-compare/          Retained website comparison material
docs/                    Current guides, plans, releases and historical archive
```

The [public product site](https://canopydigital.ca/sites/pocketmic-lan/) has a separate deployment lifecycle. Validating `dev/v3/` does not establish what is deployed there.

## Historical screenshots

![Android interface from v0.1.5](web/assets/screenshots/android-app.png)
![Windows interface from v0.1.5](web/assets/screenshots/windows-receiver.png)

These captures are from earlier v0.1.5 artifacts. They do not show the current WinUI Mica canvas, Available PCs flow or latest controls; they are retained as historical images until updated captures are available.

## Licence and support

[MIT](LICENSE). If PocketMic is useful, [buy Ryan a coffee](https://www.buymeacoffee.com/ryanspice).
