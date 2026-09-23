# PocketMic LAN v0.1.5 — Build and Release Evidence

> Release: https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.5
> Evidence refreshed: 2026-09-23 (artifact integrity and distribution metadata)
> Runtime/build evidence below remains from 2026-09-11 unless explicitly noted.

## Published artifact verification (2026-09-23)

Downloaded the tag-pinned public release assets and checked them against the attached `SHA256SUMS.txt` manifest and GitHub asset digests.

| Artifact | SHA-256 | Result |
|---|---|---|
| `app-debug.apk` | `4a770f661b094bb3dc597dc20dec88a7459efc8a7177496242e3b918adc70367` | PASS — matches manifest and GitHub asset digest |
| `PocketMicReceiver-win-x64.zip` | `593e9d23a32f29aa0562cc5af69dda9b49c9a078aa943e4f40e1673a57c7d2d9` | PASS — matches manifest and GitHub asset digest |
| `SHA256SUMS.txt` | `d6f5c286338b374abb0a6a7cf7e5ea0e0326aebfe3dec2f2c21a3d070ee02c44` | PASS — matches GitHub asset digest |

The APK signature verifies with APK Signature Scheme v2. Its signer is the Android Debug certificate (SHA-256 `1db6ab0b00cc70d19bcfce6f5160212bf1e3fc18efcb4fcb61b4fec6cf2b2c45`). This is a debug-signed APK, not a production-signed Android release.

The Windows ZIP contains `opus.dll`, `LICENSE`, and `PocketMicReceiver.exe`. The ZIP manifest hash validates; no binaries were executed during this inspection.

### Distribution references

- The actual Android release asset is named `app-debug.apk`; README, landing-page download link, install examples, and release notes now use that exact name.
- The Windows ZIP and checksum download names match the published assets.
- The Android artifact remains debug signed. Do not describe it as a production or store release.

## Build and behavioral verification (recorded 2026-09-11)

The following project checks were recorded in the original v0.1.5 build run; this refresh did not rerun builds or tests:

- Windows receiver build and 127 xUnit tests passed, including Opus smoke tests and protocol fixtures.
- Android debug build and native Opus libraries for arm64-v8a, armeabi-v7a, and x86_64 passed packaging checks.
- Protocol and jitter-buffer unit fixtures passed.
- OnePlus 9 Pro Android 14 installation was recorded as successful.

### Still requires live-device or network evidence

These checks remain open; the public release artifact/hash verification does not establish their completion:

| Check | Status | Required receipt |
|---|---|---|
| Android encoder smoke test | BLOCKED | App launch and encoder output/logcat on a physical device |
| Android-to-Windows Opus round trip | BLOCKED | End-to-end capture and decode/playback evidence |
| 30-minute screen-locked session | BLOCKED | Timed physical-device soak result |
| Packet pacing | BLOCKED | Packet capture from a real phone-to-PC session |
| Clock drift convergence | BLOCKED | Timed real-device/network measurements |

## Existing implementation evidence

The 2026-09-11 record reports Windows build/test and Android packaging results. See Git history and the v0.1.5 source tag for the full original per-check detail. Do not treat modeled bandwidth or unit-level jitter fixtures as a substitute for live audio, device lifecycle, or network measurements.

## Release checklist for the next tag

1. Freeze the candidate commit and ensure its changelog, version constants, and release tag agree.
2. Build Android and Windows artifacts from that exact commit; retain build logs and commit SHA.
3. Run unit, protocol, packaging, and required hardware/network checks. Record each result, date, environment, and evidence link; keep blockers explicit.
4. Compute SHA-256 hashes from final release files, create `SHA256SUMS.txt`, then verify the published files against it after upload.
5. Verify APK signing identity and state whether it is debug or production signed. Do not label a debug APK as a production release.
6. Publish the GitHub release with filenames copied from the actual assets; verify each tag-pinned download.
7. Update README and the PocketMic landing page version, install commands, direct asset links, compatibility notes, and checksums.
8. Check the release page, README, landing page, and all download URLs after publication; record the final evidence date and commit/tag.

## Pinned dependency reference

libopus 1.5.2 is pinned at SHA-256 `65c1d2f78b9f2fb20082c38cbe47c951ad5839345876e46941612ee87f9a7ce1`. Its BSD license source is `android/app/src/main/cpp/opus/COPYING`; the released Windows archive includes a `LICENSE` file.
