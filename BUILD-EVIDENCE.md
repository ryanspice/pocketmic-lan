# PocketMic LAN v0.1.5 — Build and Release Evidence

> Release: https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.5
> Evidence refreshed: 2026-09-23 (artifact integrity and distribution metadata)
> Runtime/build evidence below remains from 2026-09-11 unless explicitly noted.

## Android Opus selection and gain implementation (Packet C, 2026-09-24)

Working-tree implementation on top of commit `5a98afdd31a6063173c909094be6bed5f4425c77`; not yet committed or pushed at the time of these local checks.

- `testDebugUnitTest` — PASS. The Android JVM suite includes codec preference/default checks, PCM gain/clipping coverage, and Kotlin Opus input-bound cases.
- `assembleDebug lintDebug` — PASS. CMake built the JNI/libopus native targets for the configured `arm64-v8a`, `armeabi-v7a`, and `x86_64` ABIs. Lint completed successfully. Build output includes existing SDK XML/deprecation warnings, the bundled libopus non-optimized message, and existing x86_64 SIMD alignment warnings.
- Opus preference now persists through the Android UI/service path. The UI reports the codec actually used and shows PCM fallback when optional native initialization is unavailable. Opus encode failures remain explicit stream errors.
- Input gain is applied once to the captured samples before either PCM packetization or Opus encoding; JNI validates channel/frame/output bounds before encoding.
- Hosted CI, physical-device Opus initialization/encoding, Android-to-Windows Opus audio, and Android-to-Mac PCM/codec acceptance remain unverified.

### D5 hosted CI on commit `5a98afdd31a6063173c909094be6bed5f4425c77`

All push-triggered and associated pull-request workflows completed successfully:

| Event | Workflow | Result | Evidence |
|---|---|---|---|
| Push | Primary CI | PASS | [run 36078154783](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078154783) |
| Push | iOS | PASS | [run 36078154772](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078154772) |
| Push | macOS | PASS | [run 36078154757](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078154757) |
| Pull request | Primary CI | PASS | [run 36078157639](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157639) |
| Pull request | iOS | PASS | [run 36078157634](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157634) |
| Pull request | macOS | PASS | [run 36078157758](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157758) |
| Pull request | Lighthouse | PASS | [run 36078157638](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157638) |

CI confirms D5 builds and tests on the hosted matrix. Live Save-dialog export while phone traffic is active remains unverified.

## v0.1.6 development verification (2026-09-24)

These are working-tree checks, not release-candidate or published-artifact evidence. They were run on branch `codex/ios-initial-client` before the current changes were committed or pushed.

- Android: `android/gradlew.bat --max-workers=1 --no-daemon --console=plain testDebugUnitTest` — PASS. The full JVM suite completed successfully after the Auto-connect discovery reset changes. The build emitted an Android SDK XML schema-version warning and existing `MasterKeys`/`EncryptedSharedPreferences` deprecation warnings.
- Windows receiver: the updated core and regression tests previously passed 160/160; desktop build completed with 0 warnings and 0 errors. This working-tree result must be rerun by CI on the candidate commit.
- Android Auto-connect source behavior: toggling clears the saved receiver IP and restores the port default while retaining encrypted pairing credentials; discovery only runs while enabled and idle (the service may still probe during active-session reconnect); auto-fill requires a fresh, authenticated, protocol-compatible announce and stores its advertised audio port. Unit coverage now verifies nonce reset.
- Windows protocol source behavior: v2 Opus bounds are validated before decode; authenticated packets are ordered before decoder state changes; decoder teardown waits for the receive loop. These changes do not prove physical Opus audio behavior.
- Not verified by these checks: macOS driver installation/routing, physical iOS or Android audio, Android Opus native-device behavior, true pre-Start Windows receiver handshake, translation review, or release signing/distribution.

### GitHub Actions on commit `5aa8e788136789763116971be33cebdbaa1aeaf0`

Push-triggered workflows completed successfully on 2026-09-24:

| Workflow | Result | Evidence |
|---|---|---|
| Primary CI (Android build/lint/unit tests, Windows build/tests, web validation) | PASS | [run 36073518174](https://github.com/ryanspice/pocketmic-lan/actions/runs/36073518174) |
| iOS simulator build | PASS | [run 36073518225](https://github.com/ryanspice/pocketmic-lan/actions/runs/36073518225) |
| macOS build/tests/package | PASS | [run 36073518251](https://github.com/ryanspice/pocketmic-lan/actions/runs/36073518251) |
| Lighthouse validation on the associated pull request | PASS | [run 36073521359](https://github.com/ryanspice/pocketmic-lan/actions/runs/36073521359) |

The macOS result is hosted build/package evidence only. It does not establish driver installation, microphone visibility, or end-to-end audio on a physical Mac.

## Windows buffer-control fix (2026-09-24)

Working-tree implementation on top of `8fcb364`:

- Added an automatic-buffering setting, enabled by default. In automatic mode the slider is explicitly labeled as the minimum; with automatic mode off, it is the exact manual target and per-packet adaptation does not overwrite it.
- The effective playback prebuffer is displayed separately from queued audio and link-policy recommendations. Existing settings without the new field retain automatic mode through the property default.
- Link-quality assessments with insufficient packets now preserve bounds for the current tier instead of returning `0..0`; adaptive targets intersect the user's configured bounds with the tier range.
- Windows receiver core tests: **161 passed, 0 failed, 0 skipped**. Windows desktop build: **0 warnings, 0 errors**.
- GitHub Actions on commit `4a244a17760f725091f497f470c4c5203d93d0f2`: primary CI (Android build/lint/unit tests, Windows build/tests, web checks) PASS in [run 36074751559](https://github.com/ryanspice/pocketmic-lan/actions/runs/36074751559); iOS PASS in [run 36074751544](https://github.com/ryanspice/pocketmic-lan/actions/runs/36074751544); macOS PASS in [run 36074751587](https://github.com/ryanspice/pocketmic-lan/actions/runs/36074751587); pull-request Lighthouse PASS in [run 36074755655](https://github.com/ryanspice/pocketmic-lan/actions/runs/36074755655).
- Not yet verified: interactive slider/toggle behavior, actual buffer depth and audio on a running Windows receiver, or physical network quality under changing conditions. A brief live receiver smoke remains necessary before marking D1 fully accepted.

## Windows live monitor switching fix (2026-09-24)

Working-tree implementation on top of `f3dc632`:

- Changing the monitor output while the receiver is running now opens the new device before replacing the active one. If open fails, the old output and device selection remain in use; the rejected selection is not saved.
- Enabling monitoring with an unavailable or conflicting device now returns an actionable error, resets the active checkbox, and keeps the receiver audio route running. A disabled monitor selection is only saved after the device opens successfully.
- Windows receiver core tests: **161 passed, 0 failed, 0 skipped**. Windows desktop build: **0 warnings, 0 errors**.
- Live device switching and audible output remain unverified on this machine. The pushed CI run is required for this source revision, followed by a real Windows receiver smoke with a phone stream and two selectable output devices.

### D2 CI on commit `e12b57bbdd0a0ff925234c79aac745b8eb8d0e25`

- Primary Android/Windows/web CI passed: [run 36075977938](https://github.com/ryanspice/pocketmic-lan/actions/runs/36075977938).
- iOS passed: [run 36075977954](https://github.com/ryanspice/pocketmic-lan/actions/runs/36075977954). macOS passed: [run 36075977937](https://github.com/ryanspice/pocketmic-lan/actions/runs/36075977937).
- This confirms the source compiles and the existing Windows core suite runs on CI; it does not prove a physical device switch or audible monitor playback.

## Windows Custom DSP override fix (2026-09-24)

Working-tree implementation on top of `e12b57b`:

- A deliberate desktop Strength slider change now exits the phone-provided Custom mode, applies that strength to the desktop preset, and restores the preset's 85 Hz high-pass and 3.5 dB presence defaults.
- Reflecting the phone's Custom enable state does not trigger the desktop override. The UI labels the phone Custom state and tells the user that moving Strength returns to the desktop preset.
- Windows receiver core tests: **162 passed, 0 failed, 0 skipped**. Windows desktop build: **0 warnings, 0 errors**.
- Live Windows controls and listening/audio acceptance remain open for this change; hosted CI is recorded below.

### D3 CI on commit `a584f1a76d902114b3c63caf20663e579c44d801` (2026-09-25 UTC)

All push-triggered workflows and their associated pull-request workflows completed successfully:

| Event | Workflow | Result | Evidence |
|---|---|---|---|
| Push | Primary CI (Android build/lint/unit tests, Windows build/tests, web validation) | PASS | [run 36076365312](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076365312) |
| Push | iOS simulator build | PASS | [run 36076365210](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076365210) |
| Push | macOS build/tests/package | PASS | [run 36076365257](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076365257) |
| Pull request | Primary CI | PASS | [run 36076370399](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076370399) |
| Pull request | iOS simulator build | PASS | [run 36076370449](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076370449) |
| Pull request | macOS build/tests/package | PASS | [run 36076370405](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076370405) |
| Pull request | Lighthouse validation | PASS | [run 36076370441](https://github.com/ryanspice/pocketmic-lan/actions/runs/36076370441) |

These checks establish hosted build/test success for commit `a584f1a`; they do not establish live desktop interaction/audio, physical mobile interoperability, or physical Mac driver acceptance.

## Windows selected-cable microphone routing fix (D4, 2026-09-25)

Working-tree implementation on top of `a584f1a`:

- The receiver now maps only recognized exact playback/capture pairs (VB-CABLE and VoiceMeeter VAIO/AUX). Generic or ambiguous names do not enable automatic routing; changing playback selection refreshes the explanation and eligibility.
- Before routing, it snapshots Windows Console, Multimedia, and Communications capture defaults independently. A failed per-role update rolls earlier roles back. Explicit Restore restores each saved role even if the cable disappears; application exit restores only roles still pointing to the endpoint PocketMic assigned, preserving later changes to other microphones.
- Windows receiver core tests: **172 passed, 0 failed, 0 skipped**. Windows desktop build: **0 warnings, 0 errors**. `git diff --check` passed.
- Hosted push CI, iOS, and macOS passed on D4 commit `1d7f5c0540b3029a83cc9025d9da66a15cb8405b`: [CI run 36077749662](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077749662), [iOS run 36077749750](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077749750), [macOS run 36077749625](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077749625). Associated pull-request CI/iOS/macOS/Lighthouse passed in [36077752796](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077752796), [36077752850](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077752850), [36077752812](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077752812), and [36077752961](https://github.com/ryanspice/pocketmic-lan/actions/runs/36077752961).
- No Windows default microphone was changed for testing; device enumeration, actual system-default mutation/restore, destination-app audio, and unplug/replug behavior remain unverified on a live Windows audio stack.

## Windows active-session report export fix (D5, 2026-09-25)

Working-tree implementation on top of D4 commit `1d7f5c0`:

- Session report reads now open the live JSONL file with read/write sharing, snapshot its contents, and parse only complete lines. The recorder's existing write sharing remains read-only to other processes; export does not stop or mutate the recording session.
- Regression coverage exports a Markdown report while a `SessionRecorder` is still open and appending. Windows receiver core tests: **173 passed, 0 failed, 0 skipped**. Windows desktop build: **0 warnings, 0 errors**. `git diff --check` passed.
- Hosted push and pull-request CI passed: primary CI [run 36078154783](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078154783) and [run 36078157639](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157639), iOS [run 36078154772](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078154772) and [run 36078157634](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157634), macOS [run 36078154757](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078154757) and [run 36078157758](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157758), Lighthouse [run 36078157638](https://github.com/ryanspice/pocketmic-lan/actions/runs/36078157638).
- A live Save dialog export during phone traffic remains unverified.

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
