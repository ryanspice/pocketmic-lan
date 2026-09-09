# PocketMic LAN v0.1.4

PocketMic sends microphone audio from an Android phone to a Windows output destination over the same private LAN, with authenticated encryption on every packet.

The package contains source plus one-command PowerShell builds. It does not pretend that an APK or EXE was built in an environment that lacked the required Android and .NET SDKs.

## Project links

- Source: <https://github.com/ryanspice/pocketmic-lan>
- Releases: <https://github.com/ryanspice/pocketmic-lan/releases>
- Android APK: <https://github.com/ryanspice/pocketmic-lan/releases/latest/download/PocketMic-v0.1.4-debug.apk>
- Windows receiver: <https://github.com/ryanspice/pocketmic-lan/releases/latest/download/PocketMicReceiver-win-x64.zip>
- Release checksums: <https://github.com/ryanspice/pocketmic-lan/releases/latest/download/SHA256SUMS.txt>

The release URLs become live after matching assets are published with the v0.1.4 GitHub release.

## What is included

- Android transmitter: Kotlin, Jetpack Compose, foreground microphone service;
- Windows receiver: C#/.NET 8, WinForms, NAudio 2.3;
- 48 kHz mono PCM16 in 10 ms UDP packets;
- AES-256-GCM authenticated encryption;
- clean and voice-processed capture modes;
- adjustable input gain and live input level;
- selectable Windows playback device;
- LAN receiver discovery with manual address fallback;
- prebuffering, bounded loss concealment, rollover-safe sequencing, and latency trimming;
- build, firewall, protocol, and source verification tools.

## Build on Windows 11

Requirements:

- Android Studio with Android SDK Platform 36;
- Android Studio's bundled JBR or another JDK 17+;
- .NET 8 SDK;
- Python 3 is optional for the standalone verification scripts.

From the extracted package root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass

.\scripts\build-android.ps1
.\scripts\publish-windows.ps1
```

The Android script uses the committed Gradle 8.14.3 wrapper. Its default path runs the JVM unit test, Android lint, and the requested APK build.

Expected outputs:

```text
release\PocketMic-v0.1.4-debug.apk
release\PocketMicReceiver-win-x64.zip
```

Install or update the Android app:

```powershell
adb install -r .\release\PocketMic-v0.1.4-debug.apk
```

## Faster rebuilds

Keep correctness checks but skip a full clean:

```powershell
.\scripts\build-android.ps1 -SkipClean
```

Skip tests and lint only while iterating on a known local issue:

```powershell
.\scripts\build-android.ps1 -SkipClean -SkipChecks
```

Unsigned release build:

```powershell
.\scripts\build-android.ps1 -Configuration Release
```

Output:

```text
release\PocketMic-v0.1.4-release-unsigned.apk
```

## How it works

1. **Start the receiver.** Extract `PocketMicReceiver-win-x64.zip`, run `PocketMicReceiver.exe`, and confirm the UDP port and pairing key.
2. **Pair Android.** PocketMic can discover a compatible receiver on the LAN; manual IPv4 entry remains available as a fallback. Use the same pairing key on both devices.
3. **Select the destination.** Choose speakers/headphones for a direct test, or `CABLE Input` for virtual-microphone routing, then start the receiver and tap **Start microphone** on the phone.

The Android sender now stays alive when it starts before the receiver; the unconnected UDP send path does not treat an unopened PC port as a fatal stream error.

## Route into Discord, Teams, OBS, or a game

PocketMic plays into a Windows output endpoint. To expose that signal as a recording endpoint:

1. Install VB-Audio VB-CABLE.
2. Select `CABLE Input` in PocketMic Receiver.
3. Select `CABLE Output` as the microphone in the target application.

## Windows Firewall

Allow the selected UDP port on **Private networks** only. The default is `49500`. Because PocketMic
uses the next port for its authenticated control channel, the audio port must be between `1` and
`65534`.

Run an elevated PowerShell when Windows does not prompt automatically:

```powershell
.\scripts\allow-firewall.ps1
```

For a custom port:

```powershell
.\scripts\allow-firewall.ps1 -Port 49501
```

## Verification

The Android build runs its Kotlin/JVM crypto test and lint by default. Standalone checks are also included:

```powershell
python .\tools\verify_protocol.py
python .\tools\verify_receiver_logic.py
python .\tools\verify_source.py
```

See `PERFORMANCE_AUDIT.md` for the completed and pending verification scope.

## Marketing screenshot provenance

The website uses committed product captures. Rebuild both artifacts and recapture these states whenever the UI or release version changes:

- `web/assets/screenshots/android-setup.png` — captured from the debug APK on a OnePlus 9 Pro after disabling auto-connect and clearing the address field; Android system chrome was cropped, but the app UI was not altered.
- `web/assets/screenshots/windows-receiver.png` — captured from the self-contained WinForms receiver while listening on UDP 49500, waiting for a phone, with zero packet/loss counters.

Lossy mockups are not substituted for product screenshots. Rebuild both artifacts and recapture these states whenever the UI or release version changes.

## Performance profile

- Android encryption/header/packet buffers are reused in the 100-packet-per-second hot path.
- Capture and send run on separate coroutines: the capture loop never blocks on the network,
  and a bounded send queue (8 packets) drops the oldest instead of ever stalling the mic.
- Audio-source fallback now tests actual recording startup before committing to a device source.
- The default Voice mode applies exactly one processing layer — the device's own
  voice-communication source — instead of stacking software noise suppression, gain control,
  and echo cancellation on top of it.
- The input meter updates at 10 Hz rather than forcing Compose state work for every packet.
- The recorder uses Android's reported minimum or a four-packet floor instead of an
  unconditional 80 ms floor.
- Windows starts playback after a 100 ms prebuffer.
- Small gaps receive up to 200 ms of concealment: the last good frame repeated at a decaying
  level, rather than hard digital silence.
- Buffered latency is trimmed above the high-water mark (220 ms at the default prebuffer)
  instead of drifting toward the 300 ms safety ceiling.
- A removed or failed Windows output device now causes a controlled receiver shutdown instead of a false "Receiving audio" state.
- Stale faults or queued status updates from a previous receiver run cannot affect a newly started run, and UI strings are only allocated on throttled updates.
- The Wi-Fi lock mode is a policy choice fed by the live network (band, RSSI, and receiver
  loss), re-evaluated while streaming, rather than a constant selected at startup.

These settings prioritize reliable conversational latency. The prebuffer is adjustable on the
receiver; the remaining policies stay conservative until real-device measurements justify
adaptive tuning.

## Security and network boundary

- Key: `SHA-256(UTF-8(pairing key))`;
- cipher: AES-256-GCM;
- nonce: random 64-bit stream session plus unsigned 32-bit sequence;
- authenticated data: complete 24-byte protocol header;
- Android backup: disabled;
- intended scope: trusted private LAN only.

Do not port-forward the receiver. Encryption protects packet contents and integrity; it does not make this a hardened internet voice service.

## Current limits

- Windows receiver only;
- LAN discovery with manual IPv4 fallback and QR pairing;
- PCM uses more bandwidth than Opus;
- an adjustable jitter prebuffer with simple latency trimming rather than adaptive jitter/clock recovery;
- no signed Android release, Windows installer, or updater;
- end-to-end behaviour still needs physical Android/Windows/Wi-Fi/VB-CABLE testing.

## Layout

```text
android/              Android transmitter
windows-receiver/     Windows receiver
scripts/              PowerShell build and firewall helpers
tools/                Protocol, sequencing, and source verification
PERFORMANCE_AUDIT.md  Audit findings and fixes
PROTOCOL.md            Wire format and encryption
CHANGELOG.md           Version history
web/                    Static landing page and product captures
```
