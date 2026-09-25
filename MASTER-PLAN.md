# PocketMic LAN — Master Product and Release Plan

> Reviewed: 2026-09-25
> Current published release: **v0.1.5**
> Next candidate: **v0.1.6 (unreleased)**
> This plan sets product sequence and release acceptance. `ROADMAP.md` tracks milestone status; `BUILD-EVIDENCE.md` records dated build and device evidence.

## Purpose and planning rules

PocketMic LAN lets mobile devices publish microphone audio to desktop receivers over a private network. The near-term goal is to validate the new macOS receiver and mobile clients, establish a maintainable localization path, and release an honest v0.1.6 from a Windows-first development workflow using hosted CI.

- Treat merged source, current workflow results, and dated test receipts as evidence. A plan checkbox, simulator build, or CI compile does not prove physical-device audio routing.
- Do not advertise a platform, locale, codec, signing state, or distribution path beyond what has been built and accepted.
- Keep v0.1.6 bounded to the macOS receiver, reliable current mobile-to-desktop use, and an initial reviewed localization set. Keep Linux and mobile consumer roles after v0.1.6.
- Do not tag or publish v0.1.6 until its release gates below are met or the release scope is deliberately revised and documented.
- Use [`MANUAL-TESTING-0.1.6.md`](MANUAL-TESTING-0.1.6.md) to record hands-on checks against the exact expiring CI previews; any physical-device result still needs a dated tester receipt.

## Current verified state

| Area | Current state | Evidence and limitation |
|---|---|---|
| Release | v0.1.5 is the latest published release; v0.1.6 is unreleased. | [v0.1.5 release](https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.5). |
| Windows, Android, web CI | Green on current candidate commit `700b245c0ab5e0d6459838389534f24f1b553150`; Android debug build/lint/JVM tests, Windows build/full tests/package, and website release validation passed. Windows uploads a complete self-contained ZIP with pinned Opus 1.5.2. | [CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36110890990) and [Lighthouse](https://github.com/ryanspice/pocketmic-lan/actions/runs/36110890942). macOS and iOS also passed on this commit; artifact metadata is in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| Windows release package | Hosted Windows CI built x64 Opus 1.5.2, ran all 173 Windows tests including native Opus smoke coverage, and uploaded a self-contained receiver ZIP. Commit `7861f4f` adds tester setup/routing instructions plus PocketMic, NAudio, QRCoder, and Opus license notices; the publisher validates their presence. | Latest [push CI artifact](https://github.com/ryanspice/pocketmic-lan/actions/runs/36098433515/artifacts/10848212920), ZIP SHA-256 `1249ae758f575786f6c2ac2bc2fad60506022c256352ed90e31a81a008bf3b89`; artifact digest and full CI matrix are in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). This is an unsigned development preview, not a tagged release or physical Windows acceptance. |
| iOS | Simulator app builds on the current candidate; CI also runs shared PCM v1 packet-vector tests. Package contains `en-CA`, `en-US`, `ckb`, and `ku-Latn` UI catalogs plus localized microphone and local-network purpose strings. The app remains an experimental PCM v1 publisher. | [iOS CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36110890947) passed. Simulator artifact metadata is in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). No physical iPhone-to-receiver acceptance is recorded; the branch artifact is simulator-only. |
| macOS | Hosted CI built the unsigned universal app and CoreAudio virtual-microphone driver, passed protocol tests and validated the exact uploaded archives. The validator checks app/driver bundle IDs, 0.1.6 version metadata, `en-CA`, both CPU architectures, and tester install/recovery files. | [macOS CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36110890967) passed. Artifact metadata is in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). This remains build/protocol-test/package evidence, not driver installation or live routing acceptance. |
| macOS audio/protocol | Preview receiver supports encrypted PCM v1 and manual pairing, bridging audio to a 48 kHz mono CoreAudio input. It now displays a compatibility warning on structurally valid Opus v2 datagrams. One consumer; app stays foreground. | See [`macos/README.md`](macos/README.md). Select PCM on Android; Opus decode, discovery, signing/notarization, and store delivery are not included. Physical interoperability remains unverified. |
| Android audio/protocol | PCM16 is the default; Android can opt into negotiated Opus v2. The latest CI does not establish physical Opus encode/decode or end-to-end audio behavior. | Current source and the still-open items in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| Locale work | Android has `en-CA`, `en-US`, and preliminary `ckb`/`kmr` catalogs. Apple bundles verify the `en-CA` default, 38-string UI catalogs, distinct Sorani/Kurmanji resources, and localized system purpose strings. All 23 marketing HTML files declare source locale `en-CA`; core pages now offer a labelled US spelling preview. | The site preview only changes `licence`/`behaviour` spellings and is marked draft; Kurdish site translation and all other site locales remain absent. Apple en-US copy needs regional review; Kurdish drafts need fluent and script/layout review; Windows localization is unimplemented. Do not claim unreviewed locales as supported. See [`localization/README.md`](localization/README.md), [`localization/catalog-status.json`](localization/catalog-status.json), [`localization/apple/catalog-status.json`](localization/apple/catalog-status.json), and [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| Website release-source truth | Local `dev/v3` release manifest and Android/Windows download pages match the published v0.1.5 assets, SHA-256 values, and sizes; a Node validation is included in web CI. All 23 source HTML files declare `en-CA`; the core product pages also load the English regional preference control. Commit `d676fae` removes the false claim that PM-LAN is a bundled Windows virtual cable: the documented route requires separately installed VB-CABLE, with `CABLE Input` selected as the receiver output and `CABLE Output` selected in PocketMic. | Source correction and local validation passed. Hosted CI on the current branch [passed](https://github.com/ryanspice/pocketmic-lan/actions/runs/36094879660). Site copy remains English-only except the US spelling preview; Kurdish and other site translations, deployment, direct live-page verification, and physical end-to-end routing acceptance remain separate evidence gates. |
| Linux | Planned for after v0.1.6; no Linux receiver or virtual-mic implementation is part of the current build. | The Linux scope and architecture decisions are in the post-v0.1.6 plan below and [`ROADMAP.md`](ROADMAP.md). |
| Android package identity | New application ID is `com.canopydigital.pocketmic`. | Android v0.1.5 (`com.ryanspice...`) installs separately; Android data and pairing keys are not automatically migrated. |

Android Packet C was implemented in `ff690fcdcc94fca57e1a41b076bd087e5d0654ae` (`feat(android): expose Opus codec selection safely`). Platform-verified code commit `fda953e2006f38cc25791e0f9fdcfa19af861b4d` passes push and pull-request workflows for primary CI, iOS, macOS, and Lighthouse. The Windows job now builds pinned Opus 1.5.2, runs all 173 tests, and uploads a 65.2 MB self-contained ZIP with its SHA-256 recorded in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). Android Packet C has JVM tests, debug APK/native ABI build, lint, and hosted CI evidence. Apple bundle checks cover the `en-CA` default, English/Kurdish UI resources, and localized microphone/local-network purpose strings; Mac protocol tests, universal driver build, and exact archive verification pass. All source HTML pages now declare the `en-CA` language tag, but translated website copy remains open. Kurdish remains unreviewed, and hands-on audio acceptance is still open. These are development-branch checks, not a frozen release-candidate rerun; recheck workflows before tagging.

`BUILD-EVIDENCE.md` contains v0.1.5 artifact hash verification and older build evidence dated 2026-09-11. It still marks physical Android encoder smoke, Android-to-Windows Opus round trip, a 30-minute screen-locked session, packet pacing capture, and clock-drift convergence as blocked. Preserve those states until new receipts exist.

## Review findings and decisions

1. **The previous `MASTER-PLAN.md` was stale.** It described an untested Android build, missing libopus sources, 100 Windows tests, and a v0.1.5 release process as future work. Those statements no longer match the published v0.1.5 or current source/CI. This document replaces that release snapshot; the old history remains available in Git.
2. **The roadmap status was stale.** v0.1.5 is published. Remaining field checks are follow-up evidence and do not make that release “in progress.” Keep the release record historical and track remaining validation separately.
3. **macOS CI does not establish a usable virtual microphone.** A tester with a physical Mac must install and uninstall the unsigned preview driver and verify device visibility, routing through a real consumer app, audio continuity, and recovery.
4. **There is an interoperability gap to close.** Mac currently receives PCM v1, while Android supports optional Opus v2. The Mac app now surfaces structurally valid Opus v2 traffic as an unsupported-codec warning and tells the sender to select PCM; the Mac protocol guide states this explicitly. Physically test Android PCM to Mac, including a persisted Android Opus setting and the warning path, before claiming Android-to-Mac support. Do not imply that the Mac preview accepts every Android mode.
5. **Locale tags are not translated products.** The 21 entries are the first target set. Android now has separate `en-CA` and `en-US` app languages plus preliminary `ckb` (Central Kurdish/Sorani, RTL) and `kmr` (Northern Kurdish/Kurmanji, LTR) catalogs. The Android and Apple Kurdish catalogs are drafts without fluent review; Apple en-US also needs regional review, and Windows/site catalogs remain unimplemented. `en-CA` remains the default fallback and supported user/device preference takes precedence. A locale becomes supported only after critical copy is translated, reviewed, and checked on each surface that claims it.
6. **The Android package rename is a user-visible migration.** Document the separate installation and lack of automatic data/pairing-key migration before release. Decide whether this remains acceptable for v0.1.6; do not suggest the new package upgrades the old one.
7. **Signing is not configured for public store distribution.** Current Mac and iOS artifacts are unsigned previews/archives; the known Android public artifact is debug-signed. TestFlight, App Store, notarized Mac installer, and production Android release claims need their own credentials, signing setup, and successful upload/install evidence.
8. **The desktop-control review found release-relevant defects.** Its source review and runtime checks cover buffer control, monitor routing, DSP strength, Windows default-microphone switching/restoration, and exporting diagnostics during an active session. D1 now has explicit automatic/manual ownership, valid fallback bounds, a separate effective-target readout, and serialized mode settings. D2 switches monitor outputs live by opening the candidate first and retaining the current output on failure; failed devices are not selected or persisted. D3 now makes a deliberate desktop Strength adjustment leave phone Custom mode and restore desktop filter defaults. D4 now strictly maps supported playback-cable names to one matching capture endpoint, snapshots all three Windows default roles, rolls back partial changes, offers explicit per-role restore, and restores only still-owned roles on exit. D5 now exports a complete-line session snapshot while recording continues. D1-D5 pass local Windows tests/builds; D4 and D5 hosted CI are green, while D1-D4 still need live Windows interaction/audio acceptance and D5 needs a running-app Save-dialog retest.
9. **Auto-connect must perform useful preflight before microphone capture.** Android now clears stale endpoint/discovery evidence when Auto-connect changes, probes while idle when enabled, and only adopts a fresh authenticated, compatible receiver announcement and its advertised audio port. The Windows default `AutoListen` path opens the receiver engine/control listener when the app starts, allowing that probe/handshake before the phone's microphone starts; the receiver does not capture microphone audio. If Windows AutoListen is disabled, the receiver must be started manually before the phone can discover it. Code paths exist, but physical pre-Start readiness and separate delivery confirmation still need device acceptance.
10. **The existing Windows receiver and Android client have broader correctness findings.** The attached implementation handoff identifies malformed Opus bounds handling, decoder shutdown/order risks, Android codec selection/gain propagation, manual/adaptive buffer ownership, and discovery lifecycle/port negotiation. Current source changes now address the Windows malformed-Opus bounds and decoder ordering/lifetime items, plus Android Auto-connect reset, fresh authenticated discovery, stale-result expiry, and advertised-port adoption. Android Packet C is implemented: persisted PCM/Opus selection reaches the service, the UI reports actual fallback, native library load is deferred, Kotlin/JNI frame bounds are checked, and gain modifies the shared input samples before either encoder path. Local JVM tests, APK/native ABI build, lint, and hosted CI pass; physical Opus/audio interoperability remains pending. D4/D5 also have local tests/build and hosted CI, while live Windows acceptance and the D5 running-app Save-dialog retest remain open. CI green alone does not clear physical acceptance.
11. **Release claims and promotion materials need a truth pass.** The prior exposure review found stale v0.1.4 release links/metadata and ambiguity between the proposed PM-LAN name and third-party VB-CABLE. Local v3 manifest and release-facing pages now match the verified v0.1.5 release; web CI validates their versions, links, checksums, and sizes. Commit `d676fae` corrects the active home page to state that VB-CABLE is installed separately and documents its playback/capture endpoint mapping; PM-LAN remains a future route name, not a shipped driver. The correction passed the local site validator, but hosted CI, deployment, live-page behavior, and physical end-to-end routing acceptance remain unverified. Before announcing v0.1.6, verify the actual tagged Windows build follows the documented mapping, then align the published pages and assets. Treat prior live-site observations as historical until checked again.

### Integrated review tasks

- **Desktop review and implementation handoff** ([review task](codex://threads/01a0d27f-4b70-78b3-a229-ff340cdea6d6); `DESKTOP-CONTROL-REVIEW.md`, `LUNA-IMPLEMENTATION-HANDOFF.md`): findings were traced through Android/Windows source and desktop controls, then carried into Gate 2. D1-D5 are implemented with local tests/builds and applicable hosted CI; live Windows interaction/audio acceptance remains open for D1-D4, and D5 still needs the running-app Save-dialog retest. I1/I2 now clear stale discovery state, retain the pairing credential, probe while idle, expire stale results, and adopt only fresh authenticated announcements with advertised ports. Windows' default AutoListen starts the engine/control listener on app open, so no new idle-listener protocol is currently required for the default pre-phone-Start path; with AutoListen off, manually start the receiver first. Verify both paths, wrong-key/missing-receiver states, and the distinction between readiness and delivered audio on physical devices. The September 24 phone session is historical v0.1.5-device evidence: 6,006 packets in one minute, no loss/rejections, 253 queue trims, and a prior pairing-key mismatch. The installed APK was not proven to come from this checkout; Opus use, audible destination-app routing, and measured end-to-end latency were not established. Do not count that session as v0.1.6 acceptance.
- **Readiness-first exposure review** ([review task](codex://threads/01a0cee2-483f-7d02-aa3c-17001dabdd20)): resolve product truth before promotion: match release metadata, downloads, signing disclosures, codec support, and the exact shipped audio route; do not imply low latency or whole-workflow offline behavior without evidence. Once a real Android/Windows/destination-app route works, capture a genuine 15–30 second demo and run a small pilot of about five qualified testers; include the physical Mac tester separately for Mac-specific acceptance. Fix the largest repeatable setup issue, then make at most two tailored posts one at a time (Android/audio/OBS, then a maker/developer showcase), checking current venue rules immediately before posting. Measure successful routes and useful failure reports, not clicks/stars; keep AI-development discussion separate from the product pitch. These are readiness gates, not a calendar-based campaign.

## v0.1.6 — Cross-platform reach and initial localization

### Objective

Deliver a testable macOS receiver preview and validated current mobile-publisher paths, with enough reviewed localization to describe accurately. Keep Windows as the primary established desktop receiver. Continue developing from Windows and let GitHub-hosted macOS runners provide Xcode builds.

### Included scope

- macOS receiver: private-LAN encrypted audio, manual pairing, clear status/diagnostics, start/stop/recovery, and a selectable CoreAudio virtual input through the preview driver.
- Interoperability with the current iOS and Android publisher implementations. Specify codec behavior for each receiver; test Android PCM v1 and either support Android Opus v2 on Mac or explicitly select/test PCM for Mac sessions.
- CI on a hosted `macos-26` runner for macOS build, protocol/unit checks, and unsigned packaging; existing Windows, Android, web, and iOS jobs remain intact.
- Locale infrastructure and a reviewed first release set, targeting all 21 tags in `localization/target-locales.json`. If the full set cannot be translated and reviewed to the gate below, ship fewer supported locales and state exactly which ones passed.
- Version/package consistency, user-facing migration and install guidance, release notes, checksums, and verified release links.
- Fix the reviewed release-critical desktop/mobile controls and protocol handling described in [`DESKTOP-CONTROL-REVIEW.md`](DESKTOP-CONTROL-REVIEW.md) and [`LUNA-IMPLEMENTATION-HANDOFF.md`](LUNA-IMPLEMENTATION-HANDOFF.md), updating the handoff against current source as work lands.
- Preserve the Auto-connect behavior already implemented: OFF/ON clears stale derived endpoint/readiness state without clearing the pairing credential; ON begins fresh idle discovery; only a fresh authenticated, protocol-compatible announce can establish receiver readiness; actual audio delivery remains a separate state. The Windows default AutoListen path starts its receive/control engine at app launch without any Windows microphone capture; with that option off, the user must start the receiver manually before phone preflight can succeed. Verify this on devices rather than adding a new protocol/lifecycle design unless that acceptance exposes a concrete gap.

### Not included in v0.1.6

- Linux receiver, virtual audio route, packaging, or Linux distribution support.
- Android phone/tablet or iPhone/iPad as an audio consumer. Mobile remains a microphone publisher in this release.
- A promise of every device/OS role combination, multi-consumer routing, or system-wide virtual devices on platforms without accepted native routes.
- Signed/notarized/store distribution unless Apple/Android credentials and complete distribution verification are added as a consciously approved release scope.
- Treating unsigned preview code as generally safe or production-ready driver software.

### Work sequence and acceptance gates

#### Gate 1 — Freeze scope and candidate identity

- Reconcile `ROADMAP.md`, `CHANGELOG.md`, README, site, app version/build numbers, package IDs, and artifact naming against v0.1.6 scope.
- The Android package migration is now stated in both the unreleased changelog and README: `com.canopydigital.pocketmic` installs separately from v0.1.5, and app data/pairing keys do not move automatically. Gate 6 still requires a clean-install/separate-install check on a device.
- **Mac codec decision for v0.1.6: PCM v1 only.** The Mac preview does not decode Opus v2 and surfaces an incompatibility warning; keep that limitation in setup and release copy. Gate 2 must verify Android-to-Mac PCM audio and the warning when Android remains set to Opus.
- Mark each of the 21 locale targets as `target`, `translated`, `reviewed`, or `supported` (or equivalent truthful states); do not use one status to mean all four.

#### Gate 2 — Protocol and platform behavior

- Shared protocol packet fixtures now cover Android PCM v1 and Opus v2 emission, iOS PCM v1 emission, and Windows/macOS PCM v1 decoding. The canonical Python-generated JSON vectors are consumed by C# and Swift; Android consumes a `.properties` companion generated from the same script. Both mobile senders compare emitted datagrams byte-for-byte. Hosted push and PR CI passed for all platforms on `abafbb2`. This verifies deterministic framing/crypto interoperability; physical audio and Mac Opus decoding remain separate and are not implied.
- Verify Android and iOS PCM v1 publication to Windows and Mac receivers on a private LAN. For Android-to-Mac, verify the selected codec path specifically.
- On physical iPhone and Android devices, cover microphone permission grant/denial, correct/wrong pairing key, audible receive, repeated start/stop, supported background behavior, network loss/recovery, and a sustained session.
- For existing v0.1.5 Android-to-Windows follow-up, rerun the blocked checks recorded in `BUILD-EVIDENCE.md` when hardware is available. Do not hold v0.1.6 hostage to unrelated Opus/clock-drift work unless it affects a claim included in this release.
- Complete the safety fixes and trace-based regression cases in handoff packets A and B: reject malformed/oversized datagrams before slicing or decode; authenticate before stateful processing; order session/sequence checks, PLC and decode correctly; and keep native decoder lifetime owned until the receive loop exits.
- Packet C implementation is committed in `ff690fcdcc94fca57e1a41b076bd087e5d0654ae`: Opus selection persists and reaches the service, unavailable native initialization is surfaced as PCM fallback, encode failure terminates with a visible error, gain is applied identically before PCM packetization/Opus encoding, and Kotlin/JNI frame bounds are checked. Local Android unit tests, debug APK/native ABI build, lint, and hosted push/PR CI pass. Validate the Android-to-Windows Opus route and chosen Mac codec contract physically; do not infer compatibility from source or CI.
- Close the Windows desktop-control review as five separately evidenced behaviors, not a blanket "controls work" claim:
  - [ ] **D1 buffer control:** demonstrate Automatic and Manual modes in the running receiver; the manual value remains user-owned, the effective target is visible separately, and packet updates do not overwrite the slider or collapse its bounds.
  - [ ] **D2 monitor output:** change the monitor device during an active stream; the new device opens before switching, failed opens retain the current device, and toggling monitoring back on uses the selected device.
  - [ ] **D3 DSP strength:** with phone Custom DSP settings active, deliberately moving desktop Strength applies the displayed desktop preset; merely receiving phone settings does not silently override them.
  - [ ] **D4 Windows microphone route:** verify the selected playback cable maps only to its matching capture endpoint; Use/Restore handles all three Windows default roles, rolls back partial failure, and preserves role changes made by another app/user after PocketMic took ownership.
  - [ ] **D5 active report export:** save a report from the running app while packets are still arriving; verify the saved snapshot is complete and the session continues. Also cover cancel/no-session/error paths.

  Source changes and unit/build evidence exist for D1-D5; hosted D4/D5 CI is green. D1-D4 still need live Windows interaction/audio acceptance, and D5 needs the active-session Save-dialog retest. Add focused failure, rollback, and concurrency tests when changes are made.
- The known jitter-drift regression was corrected: the buffer now samples at a bounded cadence and fits the timestamped samples in chronological ring order. Deterministic positive/negative wrap and irregular-cadence tests pass; the full local Windows suite passes 176/176. This does not close the separate physical clock-drift measurement in `BUILD-EVIDENCE.md`.
- Close LAN preflight independently from audio delivery: toggle Android Auto-connect OFF then ON and confirm stale derived IP/port/readiness/session evidence clears while the pairing credential remains; confirm fresh idle discovery establishes readiness before phone Start; then separately confirm audio delivery. Repeat with Windows AutoListen on and off, a wrong key, stale saved endpoint, receiver restart, and a changed audio port. Discovery must not request microphone permission or start capture. Current source implements the idle probe and Windows default AutoListen path; the physical receipts remain open.
- Packet I1/I2 implementation is present: Android resets stale discovery evidence on Auto-connect changes, starts/stops idle probes from that preference, expires old results, retains credentials, and adopts a fresh authenticated announcement's advertised port. Windows default AutoListen starts the engine/control listener as the desktop app opens; it does not capture a Windows microphone. The physical preflight and delivery acceptance cases are listed above. No new wire version or listener architecture is justified by current source evidence; revisit protocol design only if those tests expose a specific unsupported case.
- The earlier Android reset-error assertion was reconciled with the nullable error contract; the full Android JVM suite now passes. Rerun Android and Windows suites on the frozen release candidate and retain platform/hardware acceptance separately.

#### Gate 3 — Physical Mac virtual-microphone acceptance

An external tester with a physical Mac must follow the preview instructions and return a dated receipt containing Mac model, macOS version, app/driver build, logs or screenshots, and outcomes for:

- Verify both ZIP checksums from the artifact's `SHA256SUMS.txt`; record any Gatekeeper warning. Open the app only through the documented, trust-based exception path if appropriate, and never bypass a malware/damage alert or weaken Gatekeeper/SIP.
- Run the bundle-ID-guarded driver installer; CoreAudio registers **PocketMic Virtual Mic**. This is a legacy Audio Server Plug-in, so a DriverKit system-extension approval prompt is not an acceptance requirement. Record an OS refusal as blocked instead of changing system security settings.
- Selection as the input in at least one real conferencing or recording application and audible/metered phone audio.
- A wrong pairing key produces no audio; rely on protocol fixtures for tamper/replay rejection unless the owner provides a dedicated safe test procedure. Repeated start/stop works.
- Wi-Fi/network reconnect recovers as documented; underrun/disconnect behavior is understandable.
- The confirmed uninstall script removes only the expected PocketMic driver after explicit confirmation; CoreAudio refreshes and the documented recovery procedure works.

Keep the preview unsigned and installer warnings explicit. CI success cannot pass this gate.

#### Gate 4 — Localization readiness

- Deliver translation resources across every user-facing surface included in the release: Android, iOS, macOS, Windows, and core marketing/onboarding/download/privacy/troubleshooting pages.
- Have fluent reviewers review every locale claimed as supported. Review terminology, permission/security/error text, setup steps, accessibility labels, text expansion, plural rules, language switching, and layout. Verify Arabic RTL and Kurdish scripts/direction according to locale convention.
- Add deterministic checks for malformed catalogs, missing keys/fallback coverage, and accidental untranslated UI strings where the existing tooling allows.
- Publish `en-CA` as the default fallback, while honoring an available supported device/user preference. Keep `en-US` and the two Kurdish tags separate.
- If one locale fails review or is incomplete, exclude it from the supported list; do not ship machine-only critical instructions as reviewed translations.

#### Gate 5 — Product truth, demonstration, and pre-release pilot

- Reconcile README, `dev/v3/release.json`, website/download/setup pages, preview artifacts, version labels, signing language, codecs, and routing instructions before inviting testers. State the exact build under test and describe PM-LAN, VB-CABLE, or another route only as shipped and verified. Do not imply that a preview is a release candidate.
- Remove or qualify latency/audio-quality claims without current measurement receipts. Describe only the PocketMic audio hop as local-network/no PocketMic account or relay; do not imply the user's whole destination-app workflow stays offline.
- Once the selected Android + Windows + destination-app route works, capture a genuine 15–30 second demo showing the receiver, pairing/streaming, and audio reaching the named destination app. Check every protocol, codec, and routing label against current behavior; do not add unmeasured latency overlays.
- After that route is confirmed, prepare one simple architecture image showing the tested phone → LAN → receiver → destination-app path and the codec actually used. Treat it as an explanation of that specific setup, not universal platform or codec support.
- Run a small, direct pre-release pilot of about five qualified testers against a clearly labeled preview. Collect device/OS versions, destination app, routing method, completed setup, success/failure, and reproducible friction. Recruit the physical Mac tester separately for Gate 3. Treat clicks/stars as attention, not activation.
- Fix and verify the largest repeatable setup issue found by the pilot. If the pilot does not produce a concrete issue, record the evidence and decision; do not invent work just to justify a version bump. If no truthful end-to-end route can be demonstrated, hold the release and reduce or revise scope.
- The pilot/demo sequence is a readiness gate, not a calendar campaign. Keep contributor recruitment and broad promotion out of the technical acceptance path. Goals such as ten independent successful setups, external issue reports, or a first external pull request are useful follow-up measures, not prerequisites for a bounded v0.1.6 release.

#### Gate 6 — Frozen-candidate CI, artifacts, and release decision

- Run every relevant workflow on one frozen candidate commit: Windows build/tests, Android build/lint/unit tests, iOS simulator build, macOS build/tests/package, and website checks. CI must fail on unit-test failures.
- Build all release artifacts from that exact commit; verify app versions, bundle/package identifiers, artifact names, SHA-256 manifest, and tag target agree.
- Validate the Windows ZIP from a clean CI checkout: include a concise pairing/routing guide, the PocketMic license, NAudio and QRCoder MIT notices, and the Opus license whenever `opus.dll` is shipped. Confirm the guide names the actual platform pairing options and separately installed VB-CABLE route.
- State which artifacts are unsigned, debug-signed, or production-signed. Verify clean install and documented upgrade/separate-install behavior. No store/TestFlight/notarization claims without successful signing and upload validation.
- Review unresolved `BLOCKED` checks against release scope. A blocker that undermines a v0.1.6 claim must be resolved or the claim/scope must be reduced before tagging.
- Only after Gates 1–5 pass: create the v0.1.6 tag and release, attach exact artifacts/checksums, then verify tag-pinned downloads, README/site links, and release notes. Record the final commit and post-publication receipts in `BUILD-EVIDENCE.md`.

#### Gate 7 — Post-release product truth and staged promotion

- Verify README, release manifest, website/download/setup pages, and attached assets resolve to the published tag and exact artifact checksums. Confirm the published signing, codec, routing, and migration disclosures still match the build.
- If Gate 5's demo and pilot readiness criteria passed, make at most two tailored public posts, one at a time: first to a relevant Android/audio/OBS tester audience, then to a maker/developer showcase. Recheck venue rules and format immediately before posting. Keep AI-development discussion separate and factual; defer a broader campaign until pilot results justify it.
- After the first post, review completed setup reports and repeated friction before choosing whether the second post still makes sense. Do not treat venue ratings, audience counts, stars, or download clicks as evidence of successful product use. Defer contributor-specific materials such as `CONTRIBUTING.md` or “good first issue” campaigns until there is a real contributor need.
- Do not send posts or messages from CI or automatically from the release workflow. Publishing and outreach remain separate actions; if matched links or verified route documentation break, stop promotion until corrected.

## After v0.1.6 — phases 4–7

The following work starts only after Phase 3/v0.1.6 is complete. Linux is included in this plan, but is not a v0.1.6 deliverable.

### Phase 4 — Audio and transport quality

- Revisit USB/ADB transport, heartbeat and Wi-Fi failover based on user need and measured reliability.
- Evaluate Android low-latency capture/Oboe, capture-side latency measurement, DSP options, and per-device audio profiles with controlled comparisons.
- Prioritize security hardening from a current protocol/threat review; do not adopt proposed crypto changes merely because they appeared in older plans.
- Carry forward only measured improvements with compatibility tests and clear fallback behavior.

### Phase 5 — Product and platform expansion (candidate milestone: v0.3.x)

- Implement a Linux desktop receiver with encrypted LAN receive/playback, pairing/status UX, a system-wide virtual-microphone route, and recoverable start/stop behavior.
- Before implementation, choose initial audio backend(s), supported distributions, packaging format(s), install/uninstall model, and whether the virtual route uses a PipeWire/PulseAudio/JACK/native mechanism. Validate this against current Linux APIs and target distributions.
- Add Linux CI for compilation/tests/package checks and require a physical Linux host to prove device registration and real application routing. CI alone cannot pass virtual-device acceptance.
- Expand Android phones/tablets and iPhone/iPad into consumer roles only after a shared role, discovery, pairing, codec, and routing model is agreed. Define what “consumer” means per platform and keep unsupported OS-level virtual-mic behavior out of product claims.
- Reassess multi-device, recording, and advanced routing against actual user demand; these are candidates, not automatic commitments.

### Phase 6 — Extended validation

- Grow a documented hardware/OS matrix across Android, iPhone/iPad, Windows, Mac, and Linux.
- Run network-loss/recovery, long-session, battery, latency, audio-quality, and upgrade tests on representative devices; retain dated evidence and known limits.
- Add repeatable protocol interoperability fixtures and performance baselines for each supported codec and platform pair.

### Phase 7 — Distribution and promotion

- Configure and verify production signing, notarization, installers, update and rollback behavior, and store/TestFlight flows by platform.
- Prepare privacy/security disclosures, support materials, release channels, tester onboarding, and promotion only after claims match verified behavior.
- Treat release publication and platform store submission as separate approval gates with post-upload verification.

## Risks, decisions, and owners

| Risk or decision | Required resolution | Release impact |
|---|---|---|
| Mac supports PCM v1 while Android can select Opus v2 | Implement Mac Opus or ensure UI/receiver selects and verifies PCM; document limitation. | Blocks Android-to-Mac compatibility claims. |
| Unsigned CoreAudio driver installation | Physical tester acceptance, explicit preview warning, safe install/uninstall and recovery evidence. | Blocks claiming Mac virtual mic is user-ready. |
| 21 locales exceed review capacity | Complete review for all, or reduce the supported list and retain remaining tags as targets. | Blocks broad multilingual support claim, not necessarily a smaller truthful release. |
| New Android application ID | Keep as new installation with prominent migration note, or decide on a tested migration strategy. | Blocks claiming in-place upgrade. |
| No Apple distribution credentials | Keep macOS/iOS builds as unsigned artifacts; add signing only after account/secrets and upload flow are available. | Blocks TestFlight/App Store/notarized distribution, not hosted compilation. |
| Physical devices/testers unavailable | Mark exact checks BLOCKED and narrow claims; schedule evidence collection. | CI cannot substitute for runtime acceptance. |
| Desktop-control defects or Android discovery regressions remain | Track every finding and acceptance row in the linked review/handoff; update status from current-source evidence as fixes land. | Blocks reliability claims and may block v0.1.6 where the defect affects the release path. |
| Windows route/site metadata is inconsistent | Verify the tagged artifact and live product pages before preparing demo or public copy; remove unsupported routes/latency claims. | Blocks an accurate release announcement and tester recruitment. |
| Linux audio ecosystem variance | Choose backend/distribution scope before coding; prove install and system-wide route on supported Linux hosts. | Linux release remains out of scope until its own acceptance passes. |

Named human reviewers/testers should be assigned in the issue or release checklist before each physical or language-review gate begins. Keep credentials in GitHub Actions secrets or approved secret storage; never put certificates, private keys, API keys, or passwords in the repository or build logs.

## Planning references

- [`ROADMAP.md`](ROADMAP.md) — milestone and phase tracking.
- [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md) — build, artifact, and physical-device evidence ledger.
- [`MANUAL-TESTING-0.1.6.md`](MANUAL-TESTING-0.1.6.md) — pre-tag manual test checklist and current preview links.
- [`CHANGELOG.md`](CHANGELOG.md) — release-facing changes.
- [`PROTOCOL.md`](PROTOCOL.md) — current wire protocol and compatibility constraints.
- [`macos/README.md`](macos/README.md) — tester-facing macOS preview instructions and limitations.
- [`localization/target-locales.json`](localization/target-locales.json) — initial locale target registry.
- [`DESKTOP-CONTROL-REVIEW.md`](DESKTOP-CONTROL-REVIEW.md) — Windows control review and runtime coverage limits.
- [`LUNA-IMPLEMENTATION-HANDOFF.md`](LUNA-IMPLEMENTATION-HANDOFF.md) — detailed proposed fixes and acceptance scenarios; adapt paths and verify all items against current source before implementation.
- Community facts and posting rules from the exposure review require a fresh check before posting.
