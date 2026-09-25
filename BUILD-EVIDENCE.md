# PocketMic LAN v0.1.5 — Build and Release Evidence

> Release: https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.5
> Evidence refreshed: 2026-09-25 (current release metadata and v0.1.6 hosted previews)
> Older runtime/build evidence remains dated 2026-09-11 unless explicitly noted.

## Windows jitter drift regression correction (2026-09-25)

- `AdaptiveJitterBuffer` now samples buffer depth at most once per 100 ms and retains each sample's monotonic timestamp. Drift regression reads the ring in chronological order and uses elapsed seconds, rather than assuming array order and a fixed packet cadence.
- Added deterministic Windows regressions for positive and negative trends after ring wrap and for irregular packet cadence.
- Focused drift tests: **5/5 passed**. Full Windows test suite: **176/176 passed** using the installed .NET 8 SDK.
- These synthetic regressions verify the calculation only. Physical clock-drift convergence on a real phone/PC network remains a separate blocked measurement and must not be inferred from these tests.

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

## macOS archive verification and current branch CI (2026-09-25)

- Commit `ce82d04b681418ffd88f48ae69f3882c8a861b38` adds `scripts/verify_macos_artifacts.py` to the macOS workflow before artifact upload. The hosted step extracts both ZIPs safely and validates the app/driver bundle identifiers, 0.1.6 versions, app `en-CA` development region, universal `arm64`/`x86_64` executables, tester instructions/report and third-party notices, plus shell syntax for install/uninstall scripts.
- Push workflows all passed on this commit: [macOS build, protocol tests, archive validation and upload](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094058884), [primary CI: Android build/lint/JVM tests, Windows build/tests and Web validation](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094058898), and [iOS simulator build](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094058888). Associated PR workflows passed: [macOS](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094061875), [primary CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094061959), [iOS](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094061892), and [Lighthouse](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094061876).
- Uploaded tester artifact `pocketmic-macos-preview-ce82d04b681418ffd88f48ae69f3882c8a861b38`: 488,380 bytes; artifact SHA-256 `c98f52d177f97f223ac3193bfa60cb073c9feee1fa59c02c19babacd2e7f23cc`; expires 2026-10-25. It is available in the artifacts section of the [macOS push run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094058884).
- Downloaded ZIP checksums: `PocketMic-macOS-unsigned.zip` (141,242 bytes; SHA-256 `6baaf65b6467e44c2e163153ef7ee938cecafcad6c9f6e1881cd756b87841b6d`) and `PocketMicVirtualMic-driver-unsigned.zip` (346,608 bytes; SHA-256 `32b603f27a73162eaea6ba683ff433200c62777b36618f3a4351510dcb6053e3`). The outer artifact contains the README, tester report, install/uninstall scripts, and libASPL notices/license.
- Current branch build outputs from the same commit: debug APK `pocketmic-debug-apk` (23,495,843 bytes; artifact SHA-256 `e232ee573087885f3fbccba9a1cbeafc410e4e32ff8e81795205eed1ffbd3823`; expires 2026-10-09); Windows build output `pocketmic-receiver-win-x64` (151,957 bytes; artifact SHA-256 `8064d5eb0fb86a77f85c639d3302ab06cfd7e71a0113c2efde84044f869762fd`; expires 2026-10-09); iOS simulator app `pocketmic-ios-simulator-ce82d04b681418ffd88f48ae69f3882c8a861b38` (220,714 bytes; artifact SHA-256 `2c8b7cf377e0ccabbb0b98ac3ee3f8f0a7ba57b11e8bead1bd5a94f89f72633d`; expires 2026-10-09). The APK is debug-signed, the Windows artifact is build output rather than the release ZIP, and iOS is simulator-only; none is a tagged release asset.
- This is an unsigned preview for a physical-Mac tester, not a tagged release. CI does not prove driver approval/registration, selected-app routing, audible mobile audio, reconnect, or uninstall on real hardware.

## Canadian English website source locale and refreshed preview CI (2026-09-25)

- Commit `a5d4ac05a2820d4b313bc3550cebde903326e90d` updates all 23 HTML files under `dev/v3` to declare `lang="en-CA"`. The Web validator now scans every HTML file and requires that source locale. This is language metadata and accessibility groundwork only: the text remains English-only, with no website locale selector or translated catalogs.
- Local `node scripts/validate-site-release.mjs`, `node --check scripts/validate-site-release.mjs`, and `git diff --check` passed. The first validator draft tried to read every URL route as a same-named source file; that assumption was invalid for the `get-started/` manifest route, so the final checker scans actual HTML files instead and does not alter route behavior.
- Push CI passed: [primary CI including the site locale check](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094879660), [macOS build/tests/archive validation](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094879665), and [iOS simulator build](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094879700). Pull-request workflows passed: [primary CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094883742), [macOS](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094883774), [iOS](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094883786), and [Lighthouse](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094883739).
- Current development artifacts: unsigned Mac preview `pocketmic-macos-preview-a5d4ac05a2820d4b313bc3550cebde903326e90d` (488,379 bytes; digest `2bf5e6f93ad5582f5b2115b9924652e16e12808b8b9d562c91cf6bba0fd95361`; expires 2026-10-25); Android debug APK `pocketmic-debug-apk` (23,495,846 bytes; digest `40e887d783c8e86be5f973757066bba175d6c3e31f6c22097e99f410ed6dd367`; expires 2026-10-09); Windows build output `pocketmic-receiver-win-x64` (151,975 bytes; digest `a17ea0aa4b32360b6361be7908966b0dd848e5fd1c426dea0c7f9aeae6b3b8cb`; expires 2026-10-09); iOS simulator build `pocketmic-ios-simulator-a5d4ac05a2820d4b313bc3550cebde903326e90d` (220,714 bytes; digest `c013cbb6798f4cefd107ec986b415c04f2e9d6fa347747240cfabed257fabbb2`; expires 2026-10-09). These are untagged development outputs, not published release assets.
- The Mac artifact is available to the tester through the artifacts section of [the current macOS workflow](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094879665). Physical Mac installation/routing and mobile device acceptance remain open.

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

## Apple localization packaging verification (2026-09-25)

- Commit `207c7345c74b1b76e1cc37f2c304075a6a6c11e6` sets XcodeGen `developmentLanguage: en-CA` in both Apple project specs and maps the product's Northern Kurdish/Kurmanji (`kmr`) draft to Apple's explicit `ku-Latn` catalog locale. A new bundle verifier checks `CFBundleDevelopmentRegion` and requires all 38 entries in `en-US`, `ckb`, and `ku-Latn` resources for both app products.
- Push CI passed: primary CI [run 36091885787](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091885787), iOS [run 36091885815](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091885815), and macOS [run 36091885835](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091885835). The macOS job passed all seven protocol tests, bundle localization verification, universal driver build, and preview packaging. Pull-request CI passed: primary [run 36091888546](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091888546), iOS [run 36091888378](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091888378), macOS [run 36091888503](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091888503), and Lighthouse [run 36091888458](https://github.com/ryanspice/pocketmic-lan/actions/runs/36091888458).
- The downloaded iOS simulator bundle independently passed `localization/verify_apple_bundle.py`: `CFBundleDevelopmentRegion=en-CA`, with 38 entries each in `en-US.lproj`, `ckb.lproj`, and `ku-Latn.lproj`. The downloaded macOS app archive passed the same check from `Contents/Resources`; its `Contents/Info.plist` also declares `en-CA`.
- Push artifacts: iOS simulator `pocketmic-ios-simulator-207c7345c74b1b76e1cc37f2c304075a6a6c11e6` (219,260 bytes; SHA-256 `d418a4c74423558912262d0a5f5d19c4e7bf86c6aa3472547a1bf4592175a61a`; expires 2026-10-09); macOS tester preview `pocketmic-macos-preview-207c7345c74b1b76e1cc37f2c304075a6a6c11e6` (486,670 bytes; SHA-256 `cc3091124fd51994840c4d3ff714bbf40987daeb17e0b921ebaadb9fc70cb6c3`; expires 2026-10-25).
- These are development previews, not signed device distributions or v0.1.6 release assets. Canadian English packaging is now verified in CI. `en-US` still needs regional copy review; both Kurdish catalogs still need fluent review and device layout checks. No physical device or Mac-driver acceptance is implied.

## Apple permission-prompt localization (2026-09-25)

- Commit `a30ae156084a051182f44c465365838030b13028` adds a shared `InfoPlist.xcstrings` catalog for the local-network purpose text and iOS microphone purpose text. English US entries are marked translated; Kurdish entries are marked `needs_review`. The local-network text is accurate for both sender and receiver roles. CI's bundle check now requires the purpose strings alongside all 38 UI strings.
- Push workflows passed: primary CI [run 36092864661](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092864661), iOS simulator build and bundle check [run 36092864535](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092864535), and macOS protocol tests, bundle check, universal driver build, and packaging [run 36092864551](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092864551). Pull-request workflows passed: primary CI [run 36092870480](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092870480), iOS [run 36092870486](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092870486), macOS [run 36092870474](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092870474), and Lighthouse [run 36092870476](https://github.com/ryanspice/pocketmic-lan/actions/runs/36092870476).
- Downloaded artifacts were independently inspected. The iOS simulator app contains both purpose keys in each `en-US`, `ckb`, and `ku-Latn` `InfoPlist.strings` file. The macOS app contains the localized local-network purpose key and also builds/packages successfully.
- Push artifacts: iOS simulator `pocketmic-ios-simulator-a30ae156084a051182f44c465365838030b13028` (220,714 bytes; SHA-256 `26b049d3048cbc3f6e26bcc3165caded47ed3cf925a93b41d8a13c9742495653`; expires 2026-10-09); macOS tester preview `pocketmic-macos-preview-a30ae156084a051182f44c465365838030b13028` (488,380 bytes; SHA-256 `7209aa155dec99355149c204cf68ffa6a84ca9ea5f2f355b241db9671ba1a13a`; expires 2026-10-25).
- These artifacts remain development previews: no iPhone or Mac driver installation/audio route has been accepted, and Kurdish purpose strings need fluent review. They are not signed device distributions or v0.1.6 release assets.

## Windows self-contained package and v0.1.6 hosted verification (2026-09-25)

- Commit `fda953e2006f38cc25791e0f9fdcfa19af861b4d` adds a Windows CI build of pinned Xiph Opus 1.5.2 from its checksum-verified source archive, runs the full Windows test suite with the native DLL present, and packages the receiver as a self-contained single-file ZIP. The ZIP verifier checks the executable payload and packaged Opus DLL; single-file publishing intentionally omits a separate `.deps.json`.
- Push and pull-request workflows passed on this commit:

| Event | Workflow | Result | Evidence |
|---|---|---|---|
| Push | Primary CI: Android build/lint/JVM tests; Windows build, 173 tests and package; web validation | PASS | [run 36096799338](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096799338) |
| Push | iOS simulator build and bundle validation | PASS | [run 36096799434](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096799434) |
| Push | macOS protocol tests, universal driver/package validation | PASS | [run 36096799388](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096799388) |
| Pull request | Primary CI | PASS | [run 36096803249](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096803249) |
| Pull request | iOS simulator build and bundle validation | PASS | [run 36096803264](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096803264) |
| Pull request | macOS protocol tests, universal driver/package validation | PASS | [run 36096803272](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096803272) |
| Pull request | Lighthouse validation | PASS | [run 36096803280](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096803280) |

- The Windows suite passed **173/173**, including native Opus create/decode/PLC/reset/dispose smoke tests. The publisher produced a 155.6 MB extracted payload (single-file receiver plus `opus.dll`) and a 65.2 MB ZIP with SHA-256 `e0d272b8fd439a8952f66c08615f687943cce409baa92a00f23c0921542cd363`.
- Push artifact `pocketmic-receiver-win-x64` is 68,186,426 bytes (artifact ZIP digest `b3b33e3ae20e94566be4a0156b9ed6a09b52b5da31af6d6a5d8618c4a092e59c`; expires 2026-10-25). Download it from the Artifacts section of [push CI run 36096799338](https://github.com/ryanspice/pocketmic-lan/actions/runs/36096799338). This is a branch preview, not a release asset or physical Windows acceptance.
- Push macOS preview `pocketmic-macos-preview-fda953e2006f38cc25791e0f9fdcfa19af861b4d` is 488,385 bytes (digest `86024ab9118021a4c57a1b1732ad06b2af244894e09e02524d7dfa413015718a`; expires 2026-10-25). Push iOS simulator artifact `pocketmic-ios-simulator-fda953e2006f38cc25791e0f9fdcfa19af861b4d` is 220,714 bytes (digest `5f9d07f87a540da34e988dff4e544581eb7a0b4e2b41104677dd547b09adf8bd`; expires 2026-10-09). Push Android debug APK `pocketmic-debug-apk` is 23,495,841 bytes (digest `e51d3ea50b761ac0642c1108cfde98500a87f40c61c4326eb2e54e68867f2033`; expires 2026-10-09).
- These outputs are development previews, not tagged v0.1.6 assets. Physical Android/iOS audio, Mac driver install and app routing, Windows control/audio acceptance, fluent locale review, live site verification, and release signing remain separate gates.

## Windows tester-ready package and hosted verification (2026-09-25)

- Commit `7861f4fba3743fc1d9d03f03437aca0711ae5331` adds `README-Windows.txt`, `PocketMic-LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, and `OPUS-COPYING.txt` when the native Opus decoder is included. The guide distinguishes Android QR pairing from iOS manual entry and documents the separate VB-CABLE route. The publisher fails if required documentation or the Opus license is missing from the ZIP.
- Local checks: PowerShell parser accepted `scripts/publish-windows.ps1`; `git diff --check` passed. Windows builds/tests/package validation ran in hosted CI.
- All push and pull-request workflows passed on the commit:

| Event | Workflow | Result | Evidence |
|---|---|---|---|
| Push | Primary CI: Android build/lint/JVM tests; Windows build, 173 tests and package; web validation | PASS | [run 36098433515](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098433515) |
| Push | iOS simulator build and bundle validation | PASS | [run 36098433618](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098433618) |
| Push | macOS protocol tests and universal driver/package validation | PASS | [run 36098433692](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098433692) |
| Pull request | Primary CI | PASS | [run 36098437339](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098437339) |
| Pull request | iOS simulator build and bundle validation | PASS | [run 36098437338](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098437338) |
| Pull request | macOS protocol tests and universal driver/package validation | PASS | [run 36098437393](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098437393) |
| Pull request | Lighthouse validation | PASS | [run 36098437420](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098437420) |

- Windows tests passed **173/173**. The publisher produced a 155.6 MB extracted payload and a 65.2 MB receiver ZIP with SHA-256 `1249ae758f575786f6c2ac2bc2fad60506022c256352ed90e31a81a008bf3b89`. The script validated the required ZIP entries before upload.
- Push artifact `pocketmic-receiver-win-x64` (artifact ID `10848212920`) is 68,190,661 bytes, with Actions artifact digest `303801b936e334d50f681c3249ad6526f5486941b7c858bb4af915808ff00b01`; it expires 2026-10-25. Download it from [push CI run 36098433515](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098433515).
- The artifact remains an unsigned branch preview. Physical Windows audio/control acceptance and the separate Mac and mobile acceptance gates are still open; this is not a v0.1.6 release asset.
