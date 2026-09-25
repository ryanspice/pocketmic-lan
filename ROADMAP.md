# PocketMic LAN — Roadmap

> Last updated: 2026-09-25 | Latest published: v0.1.5 | Next: v0.1.6 | Master: v1.0.0

---

## ✅ v0.1.4 — Release (COMPLETE)
- Protocol v1 specification
- Jitter buffer formal verification
- Security hardening documentation
- Performance benchmarks
- Landing page with download and troubleshooting
- GitHub Actions CI/CD
- 100 xUnit tests (Windows receiver)

---

## ✅ v0.1.5 — Performance & Quality (RELEASED 2026-09-14)

v0.1.5 is published; its implementation checklist is historical and is not an active release phase. Current source and development-branch CI verify Android Opus selection/native packaging, the Windows Opus receiver and adaptive jitter controls, and the Windows test suite. See [`BUILD-EVIDENCE.md`](BUILD-EVIDENCE.md) for the dated evidence and exact scope.

### Post-release field validation

These v0.1.5 follow-ups remain separate from the v0.1.6 release checklist. Do not advertise measurements or device behavior as verified until the listed receipt exists:

- [ ] Android Opus encoder smoke test on a physical device.
- [ ] Android-to-Windows Opus end-to-end audio round trip.
- [ ] 30-minute Android screen-locked session.
- [ ] Phone-to-PC packet pacing capture.
- [ ] Clock-drift convergence measurements on a real network.

The preliminary latency and loss figures from earlier drafts are not accepted measurement results. Use dated hardware/network receipts before making performance claims.

---

## v0.1.6 — Cross-platform Reach & Localization (EXPANDED PLAN)

### Phase 3 — Mac receiver, mobile compatibility, and global usability

This is the expanded Phase 3 release scope. Do not tag v0.1.6 until its acceptance gates below pass. Phase 3 now includes the macOS receiver, reliable interoperability with both mobile clients, and a reviewed initial localization set. The 21 locales are targets; only complete, fluent-reviewed locales may be claimed as supported. Later work stays in Phases 4–7 and starts after this release scope is complete.

- [x] Implemented a native macOS receiver preview with LAN listening, manual pairing, pairing-key security, status/diagnostics, a CoreAudio virtual microphone, and start/stop/recovery. The preview is PCM v1/manual pairing; QR, discovery, Opus, and production distribution remain outside this scope. Physical Mac install/routing acceptance is still required below.
- [x] Verify cross-language protocol interoperability using the canonical shared fixtures: Android PCM v1 and Opus v2 emission, iOS PCM v1 emission, and Windows/macOS PCM v1 decoding. Keep the Kotlin, C#, and Swift implementations byte-for-byte aligned through these tests; extracting a shared Swift package is a maintainability follow-up, not a release gate.
- [ ] Verify Android and iOS PCM v1 streams reach Windows and macOS on a private LAN. The macOS preview is PCM v1 only: verify that Android Opus v2 triggers its compatibility warning, and test Opus delivery with Windows. Mac Opus decoding is not required for v0.1.6.
- [x] Implemented and packaged an unsigned macOS Audio Server Plug-in virtual microphone path. Physical Mac acceptance for installation, device visibility, continuity, recovery, and real-app routing is still required; a CI build does not prove this gate.
- [x] Added macOS build, unit, protocol, and packaging workflows to GitHub Actions on a hosted macOS runner, so development and release builds remain possible from Windows. CI is compile/test evidence, not physical audio-device acceptance.
- [ ] Localize the user-facing surfaces included in the release across Android, iOS, macOS, Windows, and the public marketing site. The initial target list has 21 locales: Canadian English (en-CA), American English (en-US), Spanish, French, Brazilian Portuguese, German, Italian, Dutch, Polish, Turkish, Russian, Ukrainian, Arabic, Hindi, Indonesian, Japanese, Korean, Simplified Chinese, Traditional Chinese, Central Kurdish (Sorani, ckb), and Northern Kurdish (Kurmanji, kmr). Track these as targets in `localization/target-locales.json`; ship and claim only locales whose critical copy is complete and fluently reviewed on each claimed surface.
- [ ] Introduce complete locale-aware string resources/catalogs and formatting, an agreed translation glossary, locale selection/fallback rules, and an update workflow that makes additional locales data-only where practical. Include pluralization, text expansion, accessibility labels, and right-to-left layout support (especially Arabic) in acceptance. Partial implementation: Android has en-CA/en-US plus unreviewed Kurdish drafts; iOS and macOS now share a validated 38-string Apple catalog with en-CA source and en-US target slots plus review-needed ckb/kmr draft strings. Apple English regional review, fluent Kurdish review/layout checks, Windows, and site catalogs remain open.
- [ ] Establish one reviewable localization source and validation workflow for web and native catalogs, with checks for missing keys, fallback coverage, malformed locale files, and accidental untranslated UI text.
- [ ] Translate the core marketing/onboarding/download, privacy, and troubleshooting pages in the same initial locales as the apps. Keep claims and version/download metadata consistent in every locale. Expand further only when translations can be reviewed and maintained.

There is no technical “magic number” of supported languages. The 21-locale target is a bounded, reviewable first release goal; the code and content pipeline should make additional locales straightforward. `en-CA` is the default fallback, while a supported device/user locale takes precedence. Canadian English is not UK English (`en-GB`). Kurdish is represented by the two distinct locale tags above. Do not advertise a locale as supported when important setup, error, privacy, or troubleshooting text is still machine-only, missing, or unreviewed.

### Phases 4–7 — Deferred until Phase 3 is complete

- **Phase 4 — Audio and transport enhancements:** take up remaining codec, transport, DSP, or routing work after cross-platform compatibility is stable.
- **Phase 5 — Product expansion:** revisit a Linux desktop receiver, USB transport, multi-device, recording, and advanced routing after the Mac receiver and mobile clients interoperate. Linux scope includes encrypted LAN receive/playback, pairing/status UX, a system-wide virtual microphone route, packaging, and a supported-distribution policy.
- **Phase 6 — Extended validation:** continue broader device/network experiments, performance measurement, and long-session testing beyond the Phase 3 release acceptance matrix.
- **Phase 7 — Distribution and promotion:** pursue store signing, notarization, installers, wider tester outreach, and launch promotion only after artifact quality and platform behavior are verified.

These phases are deferred work, not prerequisites to expanding Phase 3 implementation. Release-critical acceptance evidence remains required for the platforms and claims included in v0.1.6.

### Future cross-device clients — after v0.1.6

- [ ] Expand mobile clients so Android phones/tablets and iPhones/iPads can act as either microphone publishers or audio consumers, with the same role model on Mac and Windows where platform audio APIs permit it.
- [ ] Build a Linux desktop receiver after v0.1.6, with the Windows/Mac receiver core workflow and a supported virtual-microphone route. Choose the initial Linux audio backend, packaging format(s), and supported distributions before implementation; require Linux CI plus physical playback/routing acceptance.
- [ ] Define one cross-platform pairing, discovery, codec, and audio-routing model before implementing the consumer role. Keep Android consumer/client work out of v0.1.6 so the Mac receiver and the current mobile-to-desktop path can be validated first.
- [ ] Validate role combinations across Android phone/tablet, iPhone/iPad, Mac, Windows, and Linux, including which platforms can capture, publish, receive, and expose a system-wide virtual device. Do not promise that every OS can expose a virtual microphone until its native extension/driver model is implemented and accepted.

### Release gates — complete before creating the v0.1.6 tag

These are acceptance checks for the release candidate, separate from feature plans below.

- [ ] Merge the user-selected cream-and-green marketing page from [Replace marketing site](codex://threads/01a0d5b9-83f5-7dc2-8fde-958d75ac661c) into the canonical `dev/v3/` site and replace the current public landing page at the existing PocketMic LAN URL as part of v0.1.6. Preserve or explicitly map support/legal/download routes, reconcile claims and exact release links, and make website CI/Lighthouse inspect the deploy payload.
- [ ] Review a staged version at desktop, tablet, and phone widths with keyboard, reduced-motion, no-JavaScript, theme, locale, and Signal Lab permission states. Preserve and hash the prior live payload; deploy the reviewed site after v0.1.6 tagged assets resolve, verify routes and downloads externally, and retain rollback until acceptance passes.
- [ ] Keep iOS labelled experimental until a physical iPhone-to-receiver session passes. Verify both iPhone-to-Windows and iPhone-to-macOS behavior before claiming broad iOS support.
- [ ] Build Android, Windows, iOS, and macOS from one frozen candidate commit. Require Android build/lint/unit tests, Windows build/tests, iOS simulator build, and macOS build/tests to pass. Android unit-test failures must fail CI.
- [ ] On physical iPhone and Android devices with Windows and macOS receivers on the same private LAN, verify permission grant/denial, correct and incorrect pairing keys, audible playback, repeated start/stop, backgrounding where supported, network loss/recovery, and a sustained session. Capture device/OS/app versions, logs, and packet or playback evidence. Keep every unrun scenario marked BLOCKED.
- [ ] Have an external tester with a physical Mac verify audio-device selection and the documented virtual-audio route. GitHub's macOS runner can compile and test protocol behavior but cannot prove real microphone/client playback or end-user routing.
- [ ] Have fluent reviewers check every locale claimed as supported from the 21 initial targets, including both Kurdish varieties if claimed, right-to-left layout, text expansion, system-language changes, critical permission/security/error copy, privacy language, and translated setup steps. Keep incomplete or unreviewed locales out of the supported-language list; the release may ship a smaller reviewed set.
- [ ] Review the existing Android-to-Windows follow-up gates in `BUILD-EVIDENCE.md`. Keep each unsupported measurement marked BLOCKED; resolve it only if v0.1.6 release claims depend on it.
- [ ] Align Android, Windows, iOS, macOS, README, localized website, release notes, download names, and the `v0.1.6` tag to the same version and commit. Build release artifacts from that candidate, record their SHA-256 values, and verify uploaded assets and tag-pinned downloads.
- [ ] Validate the new Android application ID `com.canopydigital.pocketmic` from a clean install. Document that it installs separately from v0.1.5 and that existing app data/pairing keys do not automatically migrate.
- [ ] State signing accurately. The Android release is not production-signed today, and iOS CI produces an unsigned archive. Do not claim Play Store/App Store/TestFlight availability unless signing, installation, and upload are configured and verified.
- [ ] After publication, verify GitHub release assets, checksums, README/website links, and compatibility notes against the live tagged release.

### Android Native Capture
- [ ] Oboe MMAP exclusive mode (bypass mixer)
- [ ] UNPROCESSED audio source (API 29+)
- [ ] SPSC ring buffer (wait-free, no allocations in callback)
- [ ] Capture-side latency measurement
- [ ] Compare AudioRecord vs Oboe latency

### USB Transport
- [ ] `adb reverse` TCP tunnel
- [ ] Connection heartbeat
- [ ] USB/Wi-Fi failover
- [ ] User guidance for USB debugging

### DSP Polish
- [ ] RNNoise toggle (configurable, off by default)
- [ ] AEC toggle (for speaker use)
- [ ] Per-device audio path profile

### Security Hardening
- [ ] AES-GCM-SIV (defense-in-depth, NOT replacing current)
- [ ] Authenticated handshake (HKDF-based, resist replay)
- [ ] DPAPI volume serial validation (Windows)
- [ ] Signed release APK

### Website & Docs
- [ ] Technical deep-dive page
- [ ] Privacy policy page
- [ ] Resolve the proposed PM-LAN name against the shipped route. Keep third-party VB-CABLE instructions explicit unless a PocketMic-owned Windows virtual route is implemented and tested; do not rename one product as the other.
- [ ] Version number sourced from GitHub API

---

## v0.2.0 — USB & Quality (PLANNED)

### USB Transport
- [ ] ADB reverse TCP tunnel as secondary transport
- [ ] Connection heartbeat
- [ ] USB/Wi-Fi failover
- [ ] User guidance for USB debugging

### DSP Polish
- [ ] RNNoise toggle (configurable, off by default)
- [ ] AEC toggle (for speaker use)
- [ ] Per-device audio path profile

### Security Hardening
- [ ] AES-GCM-SIV (defense-in-depth, NOT replacing current)
- [ ] Authenticated handshake (HKDF-based, resist replay)
- [ ] DPAPI volume serial validation (Windows)
- [ ] Signed release APK

### Website & Docs
- [ ] Technical deep-dive page
- [ ] Privacy policy page
- [ ] Resolve the proposed PM-LAN name against the shipped route. Keep third-party VB-CABLE instructions explicit unless a PocketMic-owned Windows virtual route is implemented and tested; do not rename one product as the other.
- [ ] Version number sourced from GitHub API

---

## v0.3.0 — Features (PLANNED)

### Multi-Device
- [ ] Multiple simultaneous connections
- [ ] Device management UI
- [ ] Per-device volume control

### Recording
- [ ] Local recording (Android)
- [ ] Remote recording (Windows)
- [ ] Export formats (WAV, MP3)

### Advanced Routing
- [ ] Audio input selection (mic, system, app)
- [ ] Audio output selection (speaker, Bluetooth, USB)
- [ ] Loopback testing

---

## v1.0.0 — Stable Release (PLANNED)

### Production Quality
- [ ] Signed Android APK (Play Store or direct)
- [ ] Windows installer (MSIX or NSIS)
- [ ] Auto-update mechanism
- [ ] Crash reporting (opt-in)
- [ ] Analytics (opt-in, privacy-respecting)

### Documentation
- [ ] User manual
- [ ] Developer guide
- [ ] API documentation
- [ ] Contributing guide

### Community
- [ ] GitHub Discussions
- [ ] Issue templates
- [ ] PR templates
- [ ] Code of conduct

---

## Research Questions (OPEN)

1. **Opus real-world bandwidth**: Does 32kbps Opus actually sound good for voice? Need device testing.
2. **Adaptive buffer convergence**: Does the P95 estimator converge fast enough for Wi-Fi roaming?
3. **Oboe vs AudioRecord**: How much latency improvement does MMAP exclusive actually give on common devices?
4. **USB transport feasibility**: Is `adb reverse` reliable enough for production use?
5. **AES-GCM-SIV**: Is the defense-in-depth worth the implementation cost?
6. **RNNoise integration**: Does it help or hurt voice quality in practice?

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-09-09 | AES-GCM nonce handling is correct | DeepSeek found existing guard implements RFC 5116 §3.1 |
| 2026-09-09 | Don't use GLM's nonce construction | XOR/truncate/hash are NOT equivalent for AES-GCM |
| 2026-09-09 | Adopt Opus over raw PCM16 | 12× bandwidth reduction, FEC + PLC for free |
| 2026-09-09 | Adaptive jitter buffer over fixed | Percentile-based adapts to network conditions |
| 2026-09-09 | Oboe over raw AudioRecord | MMAP exclusive bypasses mixer for lower latency |
| 2026-09-09 | USB transport as opt-in | Requires USB debugging, not suitable as default |
| 2026-09-09 | AES-GCM-SIV as defense-in-depth | NOT replacing current, supplementing for nonce-reuse resilience |
| 2026-09-09 | Foreground service type microphone | Android 14+ requirement for long-running mic access |
