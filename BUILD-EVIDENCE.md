# PocketMic LAN v0.1.5 — Build and Release Evidence

> Release: https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.5
> Evidence refreshed: 2026-09-25 (current release metadata and v0.1.6 hosted previews)
> Older runtime/build evidence remains dated 2026-09-11 unless explicitly noted.

## Android Opus selection and gain implementation (Packet C, 2026-09-24)

Working-tree implementation on top of commit `5a98afdd31a6063173c909094be6bed5f4425c77`; not yet committed or pushed at the time of these local checks.

- `testDebugUnitTest` — PASS. The Android JVM suite includes codec preference/default checks, PCM gain/clipping coverage, and Kotlin Opus input-bound cases.
- `assembleDebug lintDebug` — PASS. CMake built the JNI/libopus native targets for the configured `arm64-v8a`, `armeabi-v7a`, and `x86_64` ABIs. Lint completed successfully. Build output includes existing SDK XML/deprecation warnings, the bundled libopus non-optimized message, and existing x86_64 SIMD alignment warnings.
- Opus preference now persists through the Android UI/service path. The UI reports the codec actually used and shows PCM fallback when optional native initialization is unavailable. Opus encode failures remain explicit stream errors.
- Input gain is applied once to the captured samples before either PCM packetization or Opus encoding; JNI validates channel/frame/output bounds before encoding.
- Hosted CI passed on implementation commit `ff690fcdcc94fca57e1a41b076bd087e5d0654ae`:

| Event | Workflow | Result | Evidence |
|---|---|---|---|
| Push | Primary CI (Android build/lint/unit tests, Windows build/tests, web checks) | PASS | [run 36079402684](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079402684) |
| Push | iOS simulator build | PASS | [run 36079402514](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079402514) |
| Push | macOS build/tests/package | PASS | [run 36079402704](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079402704) |
| Pull request | Primary CI | PASS | [run 36079406189](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079406189) |
| Pull request | iOS simulator build | PASS | [run 36079406487](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079406487) |
| Pull request | macOS build/tests/package | PASS | [run 36079406187](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079406187) |
| Pull request | Lighthouse validation | PASS | [run 36079406185](https://github.com/ryanspice/pocketmic-lan/actions/runs/36079406185) |

- GitHub uploaded the debug APK artifact from push run 36079402684 (SHA-256 `eccc0abe4145b07fb5ea009f560fa67847ce5c4aea5504aa87567c750b91fc95`; expires 2026-10-09). Hosted Android CI and the APK artifact do not prove physical-device Opus initialization/encoding, Android-to-Windows Opus audio, or Android-to-Mac PCM/codec acceptance.

## Current-release site metadata reconciliation (2026-09-24)

- GitHub's published `v0.1.5` release lists `app-debug.apk` (35,585,121 bytes; debug-signed) and `PocketMicReceiver-win-x64.zip` (70,613,256 bytes), with SHA-256 values recorded in `dev/v3/release.json` and the release asset manifest.
- Updated the local `dev/v3` manifest, release notes, current download pages, setup/technical/troubleshooting references, and route title from stale `v0.1.4` release pointers to the verified published `v0.1.5` artifacts. Historical marketing drafts remain version-pinned to their original campaign.
- Added `scripts/validate-site-release.mjs` to verify manifest schema, release URLs, checksums, sizes, page links, route title, and matching visible version. `node scripts/validate-site-release.mjs`, `node --check web/app.js`, JSON parsing, stale-reference scan of active pages, and `git diff --check` passed locally.
- Extended the Web CI job to run this release-site validator. This is a source correction only; no live-site deployment or direct live download-page verification was performed.

## Android locale catalog and hosted validation (2026-09-25)

- Commit `01d6fe32ced1da81a4ec2ca1cd64a538aff5ab2d` adds Android app-language configuration for `en-CA`, `en-US`, Central Kurdish/Sorani (`ckb`, RTL), and Northern Kurdish/Kurmanji (`kmr`, LTR). It externalizes app, connection, diagnostics, service-error, and notification copy; adds preliminary Kurdish resources; and validates locale registration, catalog coverage, XML parsing, and format placeholders in CI. Canadian English remains the fallback. The Kurdish catalogs have not had fluent review and must not be represented as reviewed or fully supported yet. Other product-wide locale targets and iOS/macOS/Windows/site localization remain separate work.
- Hosted push workflows passed on this commit: primary CI (locale validation, Android build/lint/JVM tests, Windows build/tests, web checks) [run 36085574197](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085574197); macOS app and universal virtual-mic build/package [run 36085574169](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085574169); iOS simulator build [run 36085574232](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085574232).
- The associated pull-request workflows also passed: primary CI [run 36085704587](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085704587), macOS [run 36085704615](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085704615), iOS [run 36085704606](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085704606), and Lighthouse [run 36085704644](https://github.com/ryanspice/pocketmic-lan/actions/runs/36085704644).
- Push-run artifacts: debug APK `pocketmic-debug-apk` (23,495,839 bytes; artifact digest `b319d7804418472a37ecc806e6e68045c2ede9a609f0fe73ac46897335d3a688`; expires 2026-10-09); Windows build outputs `pocketmic-receiver-win-x64` (151,966 bytes; digest `57baa7438780dfbc6cd9a2a40d8dab3bb715b7d86db2db8502fdc52dad2a674f`; expires 2026-10-09); unsigned Mac preview `pocketmic-macos-preview-01d6fe32ced1da81a4ec2ca1cd64a538aff5ab2d` (473,080 bytes; digest `7c82c4b51e2b54ccfeb79204061b422eda4b34a86d3c80374125b51eaf16f749`; expires 2026-10-25); iOS simulator artifact `pocketmic-ios-simulator-01d6fe32ced1da81a4ec2ca1cd64a538aff5ab2d` (209,865 bytes; digest `2402ad5dee83c891000ac13cc2275ef549e7dedac5dad2fe9e17d5b5d1039c15`; expires 2026-10-09).
- These artifacts are development previews, not v0.1.6 release assets. The Android APK is debug-signed, the Mac preview is unsigned, and the iOS artifact is for the simulator. CI does not pass physical Android/iOS/Mac audio, fluent localization review, live site verification, or release-signing gates.

## v0.1.6 preview artifacts (2026-09-25)

- Commit `41fb9ad14d34ec1c4b30f3b77c541ca3cf16134e` passed push CI [run 36081845224](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081845224) and associated PR CI [run 36081849226](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081849226). Android build, lint, JVM unit tests, Windows build/tests, and the web release validator all passed. The push run uploaded `pocketmic-debug-apk` (23,481,548 bytes; artifact SHA-256 `cafb86f85f52a97b6607422502a09765974d6a4da9425722a2ddc9d88e867f66`; expires 2026-10-09) and `pocketmic-receiver-win-x64` (151,959 bytes; artifact SHA-256 `9b03e6c04adab3c65e15f332f93e4c4ab4e10b897108fccbb4a71fc88de159e6`; expires 2026-10-09). The Android APK is debug-signed; the Windows artifact contains build outputs, not the published ZIP installer package. These development artifacts are not v0.1.6 release assets.
- Commit `41fb9ad14d34ec1c4b30f3b77c541ca3cf16134e` passed the hosted macOS build, universal virtual-mic driver build, packaging, and artifact upload in [push run 36081845256](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081845256). The tester artifact is `pocketmic-macos-preview-41fb9ad14d34ec1c4b30f3b77c541ca3cf16134e` (466,929 bytes; SHA-256 `26c65697871d1dff56be541c2c9e6b8f20cf9078947190070a04dc7bb18027a7`; expires 2026-10-25). This proves compilation and packaging only; physical installation, device visibility, selected-app routing, recovery, and uninstall remain open.
- The same commit passed the iOS simulator build and uploaded `pocketmic-ios-simulator-41fb9ad14d34ec1c4b30f3b77c541ca3cf16134e` in [push run 36081845198](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081845198) (209,865 bytes; SHA-256 `e534472a380dc6f8aff1ed2867d1434cfbe905d8482faf7abb0614ac633e76b2`; expires 2026-10-09). This is a simulator app artifact, not an installable signed iPhone distribution or physical-device test.
- Pull-request iOS [run 36081849320](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081849320), macOS [run 36081849215](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081849215), and Lighthouse [run 36081849268](https://github.com/ryanspice/pocketmic-lan/actions/runs/36081849268) also passed on that commit.
- Artifacts are tied to the development branch commit above. They are previews for testing, not v0.1.6 release assets; no tag, publication, or website deployment occurred.

## macOS codec-mismatch feedback (2026-09-25)

- Commit `c20696413510ef2b32e028f043198323ae6e9a31` adds a one-time visible warning when the Mac preview receives a structurally valid Opus v2 datagram. The warning directs the user to select PCM on the sender; the receiver continues to authenticate PCM v1 before forwarding it to the virtual-mic bridge. This is header-level compatibility detection, not Opus decoding or proof that the sender is trusted.
- Hosted push CI [run 36082802007](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082802007), macOS [run 36082801975](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082801975), iOS [run 36082802002](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082802002), and associated PR CI [run 36082805512](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082805512), macOS [run 36082805596](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082805596), iOS [run 36082805520](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082805520), and Lighthouse [run 36082805485](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082805485) all passed.
- Updated Mac tester artifact: `pocketmic-macos-preview-c20696413510ef2b32e028f043198323ae6e9a31` (473,080 bytes; SHA-256 `2a0c9dc0ceb4cb127127d5e93d8a1da48ac61148cce8e8e6551c54bc3bc3e4d5`; expires 2026-10-25). It is available under the artifacts section of [push macOS run 36082801975](https://github.com/ryanspice/pocketmic-lan/actions/runs/36082801975). Physical codec-warning behavior and Android PCM-to-Mac audio remain unverified.

## macOS PCM packet sequence rollover fix (2026-09-25)

- Commit `29bbd060222f6c060bac5f2ca63a5cbedff148ec` replaces ordinary `UInt32` comparison with the signed modular sequence-delta rule used by the Windows receiver. This accepts progression across `0xFFFFFFFF` to `0` while rejecting duplicate, late, and half-range-or-larger jumps.
- Four XCTest cases passed on the hosted runner: normal progression and gaps, sequence rollover, duplicate/older packets, and large-jump rejection. The macOS job also built the unsigned app and universal CoreAudio driver and uploaded its artifact.
- Push CI passed: primary Android/Windows/web [run 36087054417](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087054417), iOS [run 36087054425](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087054425), and macOS [run 36087054435](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087054435).
- Pull-request CI passed: primary [run 36087056939](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056939), iOS [run 36087056936](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056936), macOS [run 36087056929](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056929), and Lighthouse [run 36087056931](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056931).
- Push-run Mac tester artifact: `pocketmic-macos-preview-29bbd060222f6c060bac5f2ca63a5cbedff148ec` (473,043 bytes; SHA-256 `ef970e2b064c642167c201d7c8a9d0b35bfeca20801870164fad72d0d5ea4280`; expires 2026-10-25). This is an unsigned preview, not a v0.1.6 release asset. Physical Mac installation/routing and physical mobile interoperability remain unverified.

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
- Not verified by these checks: macOS driver installation/routing, physical iOS or Android audio, Android Opus native-device behavior, physical pre-Start readiness/delivery confirmation (source paths exist through Android idle probing and Windows default AutoListen), translation review, or release signing/distribution.

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

## macOS shared PCM protocol fixtures and current v0.1.6 CI (2026-09-25)

- Commit `60beb91f2e50c56938d0ac6601eac51f3e51c234` adds the shared `tests/fixtures/vectors.json` resource to the Mac XCTest bundle and checks protocol v1 AES-GCM decryption, tampered-ciphertext rejection, and wrong-key rejection. Alongside the sequence-order tests, all **7 XCTest cases passed** on the hosted macOS runner.
- The current commit passed all push and associated pull-request workflows:

| Event | Workflow | Result | Evidence |
|---|---|---|---|
| Push | Primary CI (Android build/lint/unit tests, Windows build/tests, web validation) | PASS | [run 36087784416](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087784416) |
| Push | iOS simulator build | PASS | [run 36087784547](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087784547) |
| Push | macOS build, 7 XCTest cases, universal driver/package | PASS | [run 36087784405](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087784405) |
| Pull request | Primary CI | PASS | [run 36087786644](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087786644) |
| Pull request | iOS simulator build | PASS | [run 36087786648](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087786648) |
| Pull request | macOS build, 7 XCTest cases, universal driver/package | PASS | [run 36087786689](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087786689) |
| Pull request | Lighthouse validation | PASS | [run 36087786635](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087786635) |

- Push-run Mac tester artifact: `pocketmic-macos-preview-60beb91f2e50c56938d0ac6601eac51f3e51c234` (472,908 bytes; SHA-256 `eff1c3bc6cb17bc27af3212404dfae28ea9c1bab8901a33b688689dee684271b`; expires 2026-10-25). It is unsigned development preview output, not a v0.1.6 release asset. Physical installation, CoreAudio visibility, destination-app routing, reconnect, and uninstall are still open.
- CI proves build, unit/protocol tests, and packaging only. It does not prove physical Mac installation/routing or mobile-to-receiver audio acceptance.

## macOS tester materials and preview artifact (2026-09-25)

- Commit `4ccd1b4657530220d50f574075479ec4aa5b7f23` adds a tester-report template and includes it with the Mac app/driver preview. The template records Mac and OS, artifact commit, sender/consumer apps, codec, routing and recovery results, and evidence references; it explicitly excludes pairing keys.
- Push CI passed: primary CI [run 36088484208](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088484208), iOS simulator [run 36088484192](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088484192), and macOS build/protocol tests/packaging [run 36088484098](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088484098). The associated pull-request workflows passed: primary CI [run 36088487151](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088487151), iOS [run 36088487157](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088487157), macOS [run 36088487258](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088487258), and Lighthouse [run 36088487152](https://github.com/ryanspice/pocketmic-lan/actions/runs/36088487152).
- The push artifact `pocketmic-macos-preview-4ccd1b4657530220d50f574075479ec4aa5b7f23` is 475,777 bytes, SHA-256 digest `de2f6642c5ea967f808d16ffe065b25864322859b1da13fa6b811e0a80e68132`, and expires 2026-10-25. Downloaded artifact contents were checked: app archive, driver archive, install/uninstall scripts, `README.md`, `TESTER-REPORT.md`, license, and third-party notices are present. It remains an unsigned tester preview; physical Mac acceptance is still open.

## Apple app localization catalog (2026-09-25)

- Commit `cd64436260dc3c0850ede11853c913d767a056ac` adds a shared 38-string Apple String Catalog to the iOS and macOS targets. `en-CA` is the source/development locale; `en-US` has distinct locale entries but remains a target pending regional adaptation and review. Apple Kurdish, Windows, and website catalogs are still outstanding. `localization/apple/catalog-status.json` keeps those states explicit.
- The Apple catalog coverage validator and both Xcode builds passed on push: primary CI [run 36089688047](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089688047), iOS [run 36089688217](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089688217), and macOS [run 36089688090](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089688090). Associated pull-request workflows passed: primary CI [run 36089690940](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089690940), iOS [run 36089690948](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089690948), macOS [run 36089690988](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089690988), and Lighthouse [run 36089690963](https://github.com/ryanspice/pocketmic-lan/actions/runs/36089690963).
- The push Mac artifact `pocketmic-macos-preview-cd64436260dc3c0850ede11853c913d767a056ac` is 481,657 bytes, digest `e579023433f4e78c814eb2f65a8737b364c4d3c28e00839cc2256dfc77d1ceba`, and expires 2026-10-25. It is unsigned preview output, not a v0.1.6 release asset.
- Local checks passed: `python localization/validate_apple_catalog.py`, `python localization/validate_android_locales.py`, catalog JSON parsing, and `git diff --check`. Locale selection on actual iOS/macOS devices and fluent review remain open.
