# PocketMic LAN 0.1.6 — Manual Pre-Tag Test Plan

> Candidate code commit: `76d7ce72905b59be89045e3d98cb05b7cc7bf91e` (the app/source code is unchanged from the previous code candidate; later commits updated release documents).
> State: CI passed; manual acceptance is pending. This is a test build, not a release.

Use this checklist to test the exact previews below before freezing a release candidate. Record `PASS`, `FAIL`, `BLOCKED`, or `NOT TESTED` for each row. Do not include pairing keys, public IP addresses, or private account details in notes.

## Preview artifacts

| Platform | Preview | What it proves / limitation |
|---|---|---|
| Windows | [Receiver ZIP](https://github.com/ryanspice/pocketmic-lan/actions/runs/36112980901/artifacts/10853728561) | Self-contained Windows x64 receiver candidate. Install VB-CABLE separately for the documented virtual-cable route. |
| Android | [Debug APK](https://github.com/ryanspice/pocketmic-lan/actions/runs/36112980901/artifacts/10853648970) | Version 0.1.6, package `com.canopydigital.pocketmic`, debug-signed. Installs separately from v0.1.5; app data and pairing keys do not migrate. |
| macOS | [Unsigned preview package](https://github.com/ryanspice/pocketmic-lan/actions/runs/36112980945/artifacts/10853733480) | Universal app and virtual-mic driver with setup guide and report template. Unsigned and not notarized; use a Mac whose owner deliberately accepts this preview. |
| iOS | [Simulator app](https://github.com/ryanspice/pocketmic-lan/actions/runs/36112980929/artifacts/10854252307) | Simulator build and protocol-vector test only. It is not an installable physical-iPhone app or an IPA for device testing. |

These GitHub artifacts expire on October 9 or 25, 2026. The Mac package contains `SHA256SUMS.txt` for its app and driver archives; verify it before opening either. Stop if macOS reports malware, damage, or revoked authorization. Do not bypass those alerts or weaken Gatekeeper/SIP. The published website manifest remains on v0.1.5 until a v0.1.6 release is actually published.

## Test setup and identity

Record before testing:

- Test date/time zone and tester initials:
- Candidate commit/artifact run:
- Windows edition/build and PC model:
- Android phone model/version and PocketMic build:
- Destination app and version:
- Network setup (same private LAN, Wi-Fi band if known):
- Routing device used (speakers, headset, or separately installed VB-CABLE):

Use a private LAN where the phone and receiver can reach one another. Start with Android PCM for Mac testing. The Mac receiver supports PCM v1 only; if Android Opus v2 traffic is sent to Mac, expect its compatibility warning. Test Android Opus with Windows instead.

## A. Android to Windows route and connection readiness

| Check | Result | Notes |
|---|---|---|
| Install the 0.1.6 APK beside v0.1.5; confirm the new package installs separately and does not claim to migrate data/keys | | |
| Grant microphone permission; deny it once and confirm capture does not start, then grant it and continue | | |
| With Windows AutoListen enabled, turn Android Auto-connect off and on; confirm stale address/port/readiness clears while the pairing key remains | | |
| Confirm fresh receiver discovery/readiness before tapping phone Start; discovery must not itself request mic permission or capture audio | | |
| Start with a wrong pairing key and confirm no audio is delivered; correct the key and start successfully | | |
| Select PCM on Android and confirm audio reaches the Windows receiver | | |
| Route to a target app using the documented VB-CABLE mapping: receiver playback output to `CABLE Input`, target app microphone input to `CABLE Output` | | |
| Separately verify Android Opus v2 to Windows; record the codec reported by the receiver and any fallback/error | | |
| Stop/start repeatedly; background/lock the phone for a short check; interrupt and restore Wi-Fi | | |
| Repeat preflight with Windows AutoListen disabled, with no receiver, and after restarting the receiver; readiness and actual delivery must remain distinct | | |

Do not claim the older Opus end-to-end, 30-minute locked-screen, packet-pacing, or physical clock-drift checks as passed unless their separate evidence receipts are completed.

## B. Windows desktop controls during an active stream

Use a real stream, not only an idle window. For microphone routing changes, record current Windows default device assignments first and confirm they are restored at the end.

| Check | Result | Notes |
|---|---|---|
| **D1 Buffer:** switch Automatic and Manual; in Manual, packet updates do not move the chosen value; the effective playback target is shown separately | | |
| **D2 Monitor:** change monitor output during a stream; successful device switches live; a failed open keeps the prior working output | | |
| **D3 Voice strength:** receive phone Custom DSP settings; move desktop Strength and verify the selected desktop preset actually takes effect | | |
| **D4 Windows microphone route:** choose the matching cable endpoint; use and restore all three Windows default roles; verify unrelated later user/app changes are not overwritten on exit | | |
| **D5 Session report:** save while packets are still arriving; verify the report has complete lines and streaming continues; also check cancel/no-session behavior | | |

## C. macOS virtual microphone (tester with a physical Mac)

Follow the bundled [macOS setup guide](macos/README.md) and fill out its [tester report](macos/TESTER-REPORT.md). Use these release-critical checks:

| Check | Result | Notes |
|---|---|---|
| Verify both inner ZIPs using the bundled `SHA256SUMS.txt` | | |
| Record Gatekeeper outcome; install only if the tester intentionally trusts this exact preview | | |
| Install the driver using the guarded script; confirm **PocketMic Virtual Mic** appears in Sound → Input | | |
| Select the virtual mic in one real recording/conferencing app; send Android PCM audio and confirm meters/audible output | | |
| Send iOS PCM only if a signed/installable physical iOS build is available; the current iOS artifact is simulator-only | | |
| Send Android Opus v2 and confirm Mac reports unsupported codec clearly; do not expect Opus audio on Mac | | |
| Verify wrong pairing key produces no audio, repeated stop/start, and Wi-Fi reconnect behavior | | |
| Uninstall using the bundled confirmation script; confirm the device disappears or record the documented recovery needed | | |

Do not test modified or replayed packets. CI protocol fixtures cover ciphertext tampering and wrong-key rejection.

## D. Locale and website checks

These checks are layout and preference checks only; they do not constitute fluent translation review.

| Check | Result | Notes |
|---|---|---|
| Android starts in Canadian English (`en-CA`) and preserves that choice through restart | | |
| Switch Android among `en-CA`, `en-US`, Central Kurdish/Sorani (`ckb`), and Northern Kurdish/Kurmanji (`kmr`); check labels, fallback, script direction, truncation, and settings persistence | | |
| Inspect Kurdish permission, pairing, security, and error strings; mark wording as needing fluent review rather than approving it from this test | | |
| On core website pages, confirm Canadian English fallback and the labelled US spelling preview; verify the choice persists after navigation/reload | | |
| Confirm site copy does not present the spelling preview or Kurdish drafts as complete translations | | |

The site remains untranslated outside the limited `en-US` spelling preview. The mobile Kurdish catalogs are drafts. Do not include these locales in a supported-language claim without fluent review of critical flows on each claimed surface.

## E. Selected marketing-site replacement

The selected design and candidate history are in [Replace marketing site](codex://threads/01a0d5b9-83f5-7dc2-8fde-958d75ac661c). Test the integrated release candidate from the latest `pocketmic-website-preview` CI artifact built from the candidate commit, or a staged copy of that exact `dev/v3/` payload. The detached candidate does not prove the deploy payload is ready.

| Check | Result | Notes |
|---|---|---|
| Confirm the staged landing page is the selected PocketMic LAN design and keeps the existing canonical URL | | |
| Confirm copy matches the tested v0.1.6 build: Android/Windows behavior, accepted Mac preview limits, actual codec/routing/signing state, and no unsupported Linux/iOS-distribution or latency claims | | |
| Confirm repository/support links and release CTA resolve to PocketMic LAN; after tag creation, verify exact v0.1.6 asset names and SHA-256 values | | |
| Confirm published v0.1.5 downloads are distinguished from unsigned/experimental v0.1.6 CI previews; verify the download CTAs open the checked-in detail pages | | |
| Open every existing download, setup, policy/privacy, troubleshooting, roadmap, and release-notes route; verify each is retained or follows its documented redirect/retirement decision | | |
| From a nested page, follow the shared navigation back to the homepage use-case section; verify route, fragment, locale, and CTA links resolve | | |
| Check desktop, tablet, and 320–390 px phone layouts in light and dark themes; verify no unintended page-wide overflow, clipped controls, or overlapping content | | |
| Use keyboard-only navigation; check skip link, visible focus, navigation, accordions, language/theme controls, and reduced-motion behavior | | |
| Disable JavaScript and confirm essential product, routing, privacy, and download information remains available | | |
| Confirm `en-CA` remains the default even in an en-US browser profile; verify the en-US spelling preview is explicitly labelled and persists only after selection | | |
| Before activating Signal Lab, confirm its local-only purpose is explained; test permission grant/denial and unsupported-browser fallback without implying it transmits to PocketMic | | |
| Record the staged payload file manifest and SHA-256 values; confirm the prior live payload and rollback route are available | | |

Do not deploy the replacement before the v0.1.6 tagged assets exist and the staged page passes review. After release upload, deploy this exact reviewed payload at the existing site path, verify routes/downloads/metadata from an external browser, and roll back if any required check fails.

## F. Results and release decision

| Area | Result | Blocking issue / evidence |
|---|---|---|
| Android → Windows PCM route | | |
| Android Opus → Windows route | | |
| Windows D1–D5 controls | | |
| Android → Mac PCM and virtual-device route | | |
| Physical iPhone route (if installable build exists) | | |
| Locale layout/preferences | | |
| Marketing-site replacement and route checks | | |
| Reproducible setup friction | | |

Attach redacted screenshots/logs only when useful. Record model/OS/app versions and the exact artifact. Mark anything unavailable on the current unsigned/simulator previews as `BLOCKED`; do not turn a CI build into a hardware pass. Hold the v0.1.6 tag until release-scope failures are fixed or explicitly removed from claims, and until the final frozen-candidate artifacts and checksums are generated.
