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

## Current verified state

| Area | Current state | Evidence and limitation |
|---|---|---|
| Release | v0.1.5 is the latest published release; v0.1.6 is unreleased. | [v0.1.5 release](https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.5). |
| Windows, Android, web CI | Green on commit `29bbd060222f6c060bac5f2ca63a5cbedff148ec`; Android build/lint/JVM tests, Windows build/tests, and website release validation passed. | [Push CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087054417) and [PR CI](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056939). Build artifact names, digests, and expiry are recorded in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| iOS | Simulator build passes on commit `29bbd06`; the app remains an experimental PCM v1 publisher. | [Push iOS CI run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087054425) and [PR iOS run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056936) passed. No physical iPhone-to-receiver acceptance is recorded. |
| macOS | Hosted CI built the unsigned universal app and CoreAudio virtual-microphone driver, with four passing XCTest cases for packet progression, duplicates/late rejection, and UInt32 rollover, on commit `29bbd06`. | [Push macOS CI run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087054435) and [PR macOS CI run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36087056929) passed and uploaded the tester preview (artifact digest and expiry in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md)). This remains build/protocol-test/package evidence, not driver installation or live routing acceptance. |
| macOS audio/protocol | Preview receiver supports encrypted PCM v1 and manual pairing, bridging audio to a 48 kHz mono CoreAudio input. It now displays a compatibility warning on structurally valid Opus v2 datagrams. One consumer; app stays foreground. | See [`macos/README.md`](macos/README.md). Select PCM on Android; Opus decode, discovery, signing/notarization, and store delivery are not included. Physical interoperability remains unverified. |
| Android audio/protocol | PCM16 is the default; Android can opt into negotiated Opus v2. The latest CI does not establish physical Opus encode/decode or end-to-end audio behavior. | Current source and the still-open items in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| Locale work | Android now exposes `en-CA` (Canadian English fallback), `en-US`, `ckb` (Central Kurdish/Sorani, RTL), and `kmr` (Northern Kurdish/Kurmanji, LTR). Android catalog and placeholder validation passes in push and pull-request CI. | The Kurdish catalogs are preliminary and unreviewed; do not claim them as supported until fluent review. The other 17 product targets, iOS/macOS/Windows catalogs, and marketing-site translations remain open. See [`localization/README.md`](localization/README.md), [`localization/catalog-status.json`](localization/catalog-status.json), and [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| Website release-source truth | Local `dev/v3` release manifest and Android/Windows download pages now match the published v0.1.5 assets, SHA-256 values, and sizes; a Node validation is included in web CI. | Source-only change; no deployment occurred. Direct live download-page verification remains outstanding. |
| Linux | Planned for after v0.1.6; no Linux receiver or virtual-mic implementation is part of the current build. | The Linux scope and architecture decisions are in the post-v0.1.6 plan below and [`ROADMAP.md`](ROADMAP.md). |
| Android package identity | New application ID is `com.canopydigital.pocketmic`. | Android v0.1.5 (`com.ryanspice...`) installs separately; Android data and pairing keys are not automatically migrated. |

Android Packet C was implemented in `ff690fcdcc94fca57e1a41b076bd087e5d0654ae` (`feat(android): expose Opus codec selection safely`). The latest validated commit is `29bbd060222f6c060bac5f2ca63a5cbedff148ec`; push and associated pull-request checks for primary CI, iOS, macOS, and Lighthouse passed. The macOS receiver now uses the same signed modular sequence-delta rule as Windows, including 32-bit rollover, with four XCTest cases running in CI. Android Packet C has JVM tests, a debug APK/native ABI build, lint, and hosted CI evidence. The Android locale catalogs and format checker also pass CI, with Kurdish review still open. D4/D5 desktop changes are hosted-CI green; remaining hands-on audio acceptance is still open. See [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). These are development-branch checks, not a frozen release-candidate rerun. Recheck the live workflows before tagging.

`BUILD-EVIDENCE.md` contains v0.1.5 artifact hash verification and older build evidence dated 2026-09-11. It still marks physical Android encoder smoke, Android-to-Windows Opus round trip, a 30-minute screen-locked session, packet pacing capture, and clock-drift convergence as blocked. Preserve those states until new receipts exist.

## Review findings and decisions

1. **The previous `MASTER-PLAN.md` was stale.** It described an untested Android build, missing libopus sources, 100 Windows tests, and a v0.1.5 release process as future work. Those statements no longer match the published v0.1.5 or current source/CI. This document replaces that release snapshot; the old history remains available in Git.
2. **The roadmap status was stale.** v0.1.5 is published. Remaining field checks are follow-up evidence and do not make that release “in progress.” Keep the release record historical and track remaining validation separately.
3. **macOS CI does not establish a usable virtual microphone.** A tester with a physical Mac must install and uninstall the unsigned preview driver and verify device visibility, routing through a real consumer app, audio continuity, and recovery.
4. **There is an interoperability gap to close.** Mac currently receives PCM v1, while Android supports optional Opus v2. The Mac app now surfaces structurally valid Opus v2 traffic as an unsupported-codec warning and tells the sender to select PCM; the Mac protocol guide states this explicitly. Physically test Android PCM to Mac, including a persisted Android Opus setting and the warning path, before claiming Android-to-Mac support. Do not imply that the Mac preview accepts every Android mode.
5. **Locale tags are not translated products.** The 21 entries are the first target set. Android now has separate `en-CA` and `en-US` app languages plus preliminary `ckb` (Central Kurdish/Sorani, RTL) and `kmr` (Northern Kurdish/Kurmanji, LTR) catalogs. The Kurdish copy has not had fluent review, and broader iOS/macOS/Windows/site localization is not implemented. `en-CA` remains the default fallback and supported user/device preference takes precedence. A locale becomes supported only after critical copy is translated, reviewed, and checked on each surface that claims it.
6. **The Android package rename is a user-visible migration.** Document the separate installation and lack of automatic data/pairing-key migration before release. Decide whether this remains acceptable for v0.1.6; do not suggest the new package upgrades the old one.
7. **Signing is not configured for public store distribution.** Current Mac and iOS artifacts are unsigned previews/archives; the known Android public artifact is debug-signed. TestFlight, App Store, notarized Mac installer, and production Android release claims need their own credentials, signing setup, and successful upload/install evidence.
8. **The desktop-control review found release-relevant defects.** Its source review and runtime checks cover buffer control, monitor routing, DSP strength, Windows default-microphone switching/restoration, and exporting diagnostics during an active session. D1 now has explicit automatic/manual ownership, valid fallback bounds, a separate effective-target readout, and serialized mode settings. D2 switches monitor outputs live by opening the candidate first and retaining the current output on failure; failed devices are not selected or persisted. D3 now makes a deliberate desktop Strength adjustment leave phone Custom mode and restore desktop filter defaults. D4 now strictly maps supported playback-cable names to one matching capture endpoint, snapshots all three Windows default roles, rolls back partial changes, offers explicit per-role restore, and restores only still-owned roles on exit. D5 now exports a complete-line session snapshot while recording continues. D1-D5 pass local Windows tests/builds; D4 and D5 hosted CI are green, while D1-D4 still need live Windows interaction/audio acceptance and D5 needs a running-app Save-dialog retest.
9. **Auto-connect must perform useful preflight before microphone capture.** Android now clears stale endpoint/discovery evidence when Auto-connect changes, probes while idle when enabled, and only adopts a fresh authenticated, compatible receiver announcement and its advertised audio port. The Windows default `AutoListen` path opens the receiver engine/control listener when the app starts, allowing that probe/handshake before the phone's microphone starts; the receiver does not capture microphone audio. If Windows AutoListen is disabled, the receiver must be started manually before the phone can discover it. Code paths exist, but physical pre-Start readiness and separate delivery confirmation still need device acceptance.
10. **The existing Windows receiver and Android client have broader correctness findings.** The attached implementation handoff identifies malformed Opus bounds handling, decoder shutdown/order risks, Android codec selection/gain propagation, manual/adaptive buffer ownership, and discovery lifecycle/port negotiation. Current source changes now address the Windows malformed-Opus bounds and decoder ordering/lifetime items, plus Android Auto-connect reset, fresh authenticated discovery, stale-result expiry, and advertised-port adoption. Android Packet C is implemented: persisted PCM/Opus selection reaches the service, the UI reports actual fallback, native library load is deferred, Kotlin/JNI frame bounds are checked, and gain modifies the shared input samples before either encoder path. Local JVM tests, APK/native ABI build, lint, and hosted CI pass; physical Opus/audio interoperability remains pending. D4/D5 also have local tests/build and hosted CI, while live Windows acceptance and the D5 running-app Save-dialog retest remain open. CI green alone does not clear physical acceptance.
11. **Release claims and promotion materials need a truth pass.** The prior exposure review found stale v0.1.4 release links/metadata and ambiguity between the proposed PM-LAN name and third-party VB-CABLE. Local v3 manifest and release-facing pages now match the verified v0.1.5 release; web CI validates their versions, links, checksums, and sizes. No site deployment occurred, and live download-page behavior plus the shipped Windows routing path still require verification before announcing v0.1.6. Treat prior live-site observations as historical until checked again.

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
- Record the Android package migration notice and confirm it is clear that v0.1.5 app data will not move automatically.
- Decide the Mac codec contract: support Opus v2, or expose/document and test PCM mode for Mac pairing. Make protocol capability visible enough that incompatible combinations cannot be mistaken for a successful session.
- Mark each of the 21 locale targets as `target`, `translated`, `reviewed`, or `supported` (or equivalent truthful states); do not use one status to mean all four.

#### Gate 2 — Protocol and platform behavior

- Add cross-language protocol fixtures for framing, key derivation, AES-GCM fields, valid packets, wrong-key/tamper rejection, and sequence boundary behavior where missing.
- Verify Android and iOS PCM v1 publication to Windows and Mac receivers on a private LAN. For Android-to-Mac, verify the selected codec path specifically.
- On physical iPhone and Android devices, cover microphone permission grant/denial, correct/wrong pairing key, audible receive, repeated start/stop, supported background behavior, network loss/recovery, and a sustained session.
- For existing v0.1.5 Android-to-Windows follow-up, rerun the blocked checks recorded in `BUILD-EVIDENCE.md` when hardware is available. Do not hold v0.1.6 hostage to unrelated Opus/clock-drift work unless it affects a claim included in this release.
- Complete the safety fixes and trace-based regression cases in handoff packets A and B: reject malformed/oversized datagrams before slicing or decode; authenticate before stateful processing; order session/sequence checks, PLC and decode correctly; and keep native decoder lifetime owned until the receive loop exits.
- Packet C implementation is committed in `ff690fcdcc94fca57e1a41b076bd087e5d0654ae`: Opus selection persists and reaches the service, unavailable native initialization is surfaced as PCM fallback, encode failure terminates with a visible error, gain is applied identically before PCM packetization/Opus encoding, and Kotlin/JNI frame bounds are checked. Local Android unit tests, debug APK/native ABI build, lint, and hosted push/PR CI pass. Validate the Android-to-Windows Opus route and chosen Mac codec contract physically; do not infer compatibility from source or CI.
- Complete remaining handoff work for clock-drift wrap correctness, deliberate DSP preset ownership, live monitor-device changes, and acceptance of default-device routing. D1's automatic/manual buffer mode, D4's exact cable mapping/role-safe transaction, and D5's live-log export are implemented with local tests/build evidence; hosted D4/D5 CI is green. D1-D4 still need live Windows UI/audio acceptance; D5 needs a running-app Save-dialog retest. Add bounded tests around failure, rollback, and concurrency behavior.
- Close physical acceptance for packet I1/I2: Android now resets stale discovery evidence on Auto-connect changes, starts/stops idle probes from that preference, expires old results, retains credentials, and adopts a fresh authenticated announcement's advertised port. Windows default AutoListen starts the engine/control listener as the desktop app opens; this enables the pre-phone-Start probe without starting phone capture and does not itself capture a microphone on Windows. Verify receiver readiness before pressing phone Start, then separately confirm audio delivery; repeat with AutoListen disabled (manual receiver Start), wrong key, stale saved endpoint, receiver restart, and changed port. No new wire version or listener architecture is justified by current source evidence; revisit protocol design only if these tests expose a specific unsupported case. Discovery must not request microphone permission, open phone capture, or stream audio.
- The earlier Android reset-error assertion was reconciled with the nullable error contract; the full Android JVM suite now passes. Rerun Android and Windows suites on the frozen release candidate and retain platform/hardware acceptance separately.

#### Gate 3 — Physical Mac virtual-microphone acceptance

An external tester with a physical Mac must follow the preview instructions and return a dated receipt containing Mac model, macOS version, app/driver build, logs or screenshots, and outcomes for:

- Installation and explicit system approval; CoreAudio registers **PocketMic Virtual Mic**.
- Selection as the input in at least one real conferencing or recording application and audible/metered phone audio.
- Wrong key and modified/replayed datagrams do not produce audio; repeated start/stop works.
- Wi-Fi/network reconnect recovers as documented; underrun/disconnect behavior is understandable.
- Uninstall removes the driver and the documented recovery procedure works.

Keep the preview unsigned and installer warnings explicit. CI success cannot pass this gate.

#### Gate 4 — Localization readiness

- Deliver translation resources across every user-facing surface included in the release: Android, iOS, macOS, Windows, and core marketing/onboarding/download/privacy/troubleshooting pages.
- Have fluent reviewers review every locale claimed as supported. Review terminology, permission/security/error text, setup steps, accessibility labels, text expansion, plural rules, language switching, and layout. Verify Arabic RTL and Kurdish scripts/direction according to locale convention.
- Add deterministic checks for malformed catalogs, missing keys/fallback coverage, and accidental untranslated UI strings where the existing tooling allows.
- Publish `en-CA` as the default fallback, while honoring an available supported device/user preference. Keep `en-US` and the two Kurdish tags separate.
- If one locale fails review or is incomplete, exclude it from the supported list; do not ship machine-only critical instructions as reviewed translations.

#### Gate 5 — Frozen-candidate CI, artifacts, and release decision

- Run every relevant workflow on one frozen candidate commit: Windows build/tests, Android build/lint/unit tests, iOS simulator build, macOS build/tests/package, and website checks. CI must fail on unit-test failures.
- Build all release artifacts from that exact commit; verify app versions, bundle/package identifiers, artifact names, SHA-256 manifest, and tag target agree.
- State which artifacts are unsigned, debug-signed, or production-signed. Verify clean install and documented upgrade/separate-install behavior. No store/TestFlight/notarization claims without successful signing and upload validation.
- Review unresolved `BLOCKED` checks against release scope. A blocker that undermines a v0.1.6 claim must be resolved or the claim/scope must be reduced before tagging.
- Only after these gates pass: create the v0.1.6 tag and release, attach exact artifacts/checksums, then verify tag-pinned downloads, README/site links, and release notes. Record the final commit and post-publication receipts in `BUILD-EVIDENCE.md`.

#### Gate 6 — Product truth and staged tester exposure

- Before announcing availability, verify that README, `dev/v3/release.json`, website/download/setup pages, release assets, version labels, signing language, codecs, and routing instructions all refer to the same published build. Confirm the exact included Windows route; describe PM-LAN, VB-CABLE, or both only as shipped and tested.
- Remove or qualify latency/audio-quality claims without current measurement receipts. Describe only the PocketMic audio hop as local-network/no PocketMic account or relay; do not imply the user's whole destination-app workflow stays offline.
- Record one genuine 15–30 second end-to-end demo after the selected path works, showing receiver open, phone pairing/streaming, and audio reaching the named destination app. Prepare an architecture image only after protocol/codec/routing labels are checked against the release. Do not add unmeasured latency overlays.
- Recruit a small pilot of about five qualified testers for a complete Android + Windows + destination-app route, and recruit the physical Mac tester separately for Mac-specific gates. Collect device/OS versions, destination app, routing method, success/failure, and reproducible setup friction. Count successful setup reports and recurring issues; treat clicks/stars as attention, not activation.
- After the largest repeatable setup issue is fixed, make two community-specific posts one at a time: first to a relevant Android/audio/OBS tester audience, then to one maker/developer showcase. Recheck each venue's current rules and thread format before posting. Keep AI-tool process discussion separate and factual; defer broad multi-community campaigns until pilot results justify them.
- The pilot and posts are gated by matched version/download/routing instructions and the product-truth audit above. If those prerequisites are not met, keep exposure to direct, clearly labeled preview testing; do not substitute a polished demo or broad launch copy for acceptance evidence.
- Treat publishing the release and contacting communities as separate actions. Do not send posts or messages as part of CI or automatically from the release workflow.

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
- [`CHANGELOG.md`](CHANGELOG.md) — release-facing changes.
- [`PROTOCOL.md`](PROTOCOL.md) — current wire protocol and compatibility constraints.
- [`macos/README.md`](macos/README.md) — tester-facing macOS preview instructions and limitations.
- [`localization/target-locales.json`](localization/target-locales.json) — initial locale target registry.
- [`DESKTOP-CONTROL-REVIEW.md`](DESKTOP-CONTROL-REVIEW.md) — Windows control review and runtime coverage limits.
- [`LUNA-IMPLEMENTATION-HANDOFF.md`](LUNA-IMPLEMENTATION-HANDOFF.md) — detailed proposed fixes and acceptance scenarios; adapt paths and verify all items against current source before implementation.
- Community facts and posting rules from the exposure review require a fresh check before posting.
