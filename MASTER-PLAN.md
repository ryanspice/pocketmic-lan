# PocketMic LAN — Master Product and Release Plan

> Reviewed: 2026-09-24
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
| Windows, Android, web CI | Green on the latest implementation commit referenced below. | [Primary CI run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36067955866) includes Android build/lint/unit tests and Windows build/tests. |
| iOS | Simulator build passes; the app remains an experimental PCM v1 publisher. | [iOS CI run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36067955873). No physical iPhone-to-receiver acceptance is recorded. |
| macOS | Hosted CI builds unsigned universal app and CoreAudio virtual-microphone driver artifacts. | [macOS CI run](https://github.com/ryanspice/pocketmic-lan/actions/runs/36067955901). This is build/package evidence only, not driver installation or live routing acceptance. |
| macOS audio/protocol | Preview receiver supports encrypted PCM v1 and manual pairing, bridging audio to a 48 kHz mono CoreAudio input. One consumer; app stays foreground. | See [`macos/README.md`](macos/README.md). Opus v2, discovery, signing/notarization and store delivery are not included in the preview. |
| Android audio/protocol | PCM16 is the default; Android can opt into negotiated Opus v2. The latest CI does not establish physical Opus encode/decode or end-to-end audio behavior. | Current source and the still-open items in [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md). |
| Locale work | A 21-locale target registry exists, including Canadian/American English and Central/Northern Kurdish. | [`localization/target-locales.json`](localization/target-locales.json) lists targets, not completed translations. |
| Linux | Planned for after v0.1.6; no Linux receiver or virtual-mic implementation is part of the current build. | The Linux scope and architecture decisions are in the post-v0.1.6 plan below and [`ROADMAP.md`](ROADMAP.md). |
| Android package identity | New application ID is `com.canopydigital.pocketmic`. | Android v0.1.5 (`com.ryanspice...`) installs separately; Android data and pairing keys are not automatically migrated. |

The latest implementation commit is `783e9eac21789345f1678a16ade7d38e96e479c4` (`feat(macos): add virtual microphone preview and CI`). The primary CI, iOS, macOS, and Lighthouse runs for that commit passed. Recheck live GitHub run state before cutting a release; these links are the recorded evidence, not a substitute for a release-candidate rerun.

`BUILD-EVIDENCE.md` contains v0.1.5 artifact hash verification and older build evidence dated 2026-09-11. It still marks physical Android encoder smoke, Android-to-Windows Opus round trip, a 30-minute screen-locked session, packet pacing capture, and clock-drift convergence as blocked. Preserve those states until new receipts exist.

## Review findings and decisions

1. **The previous `MASTER-PLAN.md` was stale.** It described an untested Android build, missing libopus sources, 100 Windows tests, and a v0.1.5 release process as future work. Those statements no longer match the published v0.1.5 or current source/CI. This document replaces that release snapshot; the old history remains available in Git.
2. **The roadmap status was stale.** v0.1.5 is published. Remaining field checks are follow-up evidence and do not make that release “in progress.” Keep the release record historical and track remaining validation separately.
3. **macOS CI does not establish a usable virtual microphone.** A tester with a physical Mac must install and uninstall the unsigned preview driver and verify device visibility, routing through a real consumer app, audio continuity, and recovery.
4. **There is an interoperability gap to close.** Mac currently receives PCM v1, while Android supports optional Opus v2. Before claiming Android-to-Mac support, either add and test Opus v2 on Mac or define and test a dependable PCM selection/fallback for Mac sessions. Do not silently imply that the Mac preview accepts every Android mode.
5. **Locale tags are not translated products.** The 21 entries are the first target set. English variants and Kurdish varieties must remain distinct; `en-CA` is the fallback, supported user/device locale takes precedence, and `en-US` is explicit. Kurdish uses `ckb` (Central Kurdish/Sorani) and `kmr` (Northern Kurdish/Kurmanji). A locale becomes supported only after critical app and site copy is translated, reviewed, and checked, including RTL where applicable.
6. **The Android package rename is a user-visible migration.** Document the separate installation and lack of automatic data/pairing-key migration before release. Decide whether this remains acceptable for v0.1.6; do not suggest the new package upgrades the old one.
7. **Signing is not configured for public store distribution.** Current Mac and iOS artifacts are unsigned previews/archives; the known Android public artifact is debug-signed. TestFlight, App Store, notarized Mac installer, and production Android release claims need their own credentials, signing setup, and successful upload/install evidence.
8. **The desktop-control review found release-relevant defects.** Its source review and runtime checks cover buffer control, monitor routing, DSP strength, Windows default-microphone switching/restoration, and exporting diagnostics during an active session. A current-source spot check still found the buffer slider wired straight to the effective target, a global virtual-capture-device lookup, and decoder disposal before the receive task is awaited. Fix or retest each finding against current source; the prior review receipt is not proof of the current runtime.
9. **Auto-connect must perform useful preflight before microphone capture.** The user-requested behavior is to clear stale discovered endpoint/readiness when Auto-connect changes, begin a fresh LAN discovery when enabled, and distinguish a discovered receiver from an authenticated/ready receiver and from actual audio delivery. Keep the pairing credential; never broadcast it or start microphone capture just for discovery.
10. **The existing Windows receiver and Android client have broader correctness findings.** The attached implementation handoff identifies malformed Opus bounds handling, decoder shutdown/order risks, Android codec selection/gain propagation, manual/adaptive buffer ownership, and discovery lifecycle/port negotiation. Current source changes now address the Windows malformed-Opus bounds and decoder ordering/lifetime items, plus Android Auto-connect reset, fresh authenticated discovery, stale-result expiry, and advertised-port adoption. Android JVM tests pass after the nullable reset-error contract was reconciled. The Android codec path and Windows control findings D1-D5 remain open; CI green alone does not clear physical acceptance.
11. **Release claims and promotion materials need a truth pass.** A prior exposure review found conflicting version/download and virtual-audio-routing descriptions, unsupported latency wording, and ambiguity between the built-in PM-LAN route and VB-CABLE. Revalidate README, site release manifest, setup/download pages, and the actual shipped Windows routing path before announcing v0.1.6. Treat prior live-site observations as historical until checked again.

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
- Make Auto-connect transitions reset stale discovery state, restart one fresh discovery attempt when enabled, and perform a preliminary receiver handshake before microphone Start. Keep receiver discovery, pairing/authentication, readiness, and confirmed audio delivery as separate user-visible states.

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
- Complete handoff packet C's actual Android Opus selection path and input-gain parity, with explicit unavailable/encode-failure outcomes. Validate the Android-to-Windows Opus route and the chosen Mac codec contract; do not infer compatibility from Android's current PCM default.
- Complete packets D through H for truthful buffer mode/effective-target display, clock-drift wrap correctness, deliberate DSP preset ownership, live monitor-device changes, selected-cable-only microphone switching with reversible per-role defaults, and export during an active session. Add bounded tests around failure, rollback, and concurrency behavior.
- Finish packet I1/I2: the Android client now resets endpoint and discovery evidence when Auto-connect changes, starts/stops idle discovery from that preference, expires stale results, retains the pairing credential, and adopts only a fresh authenticated receiver's advertised port. The Windows control listener still starts with the audio engine, so a true pre-Start authenticated handshake requires a deliberate receiver lifecycle/protocol design. Any rendezvous/capability extension must be versioned and documented in `PROTOCOL.md`, preserve bounded authenticated frames and manual fallback, handle the shared/default control-port case explicitly, and never report an unauthenticated candidate as ready. Discovery must not request microphone permission, open capture, or start streaming.
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
