# PocketMic LAN — Roadmap

> Last updated: 2026-09-24 | Latest published: v0.1.5 | Next: v0.1.6 | Master: v1.0.0

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

v0.1.5 is published. The checklist below preserves its original implementation scope; its unchecked hardware and measurement items are follow-up evidence, not a claim that the release is still in progress. See `BUILD-EVIDENCE.md` for the checks that remain blocked and `MASTER-PLAN.md` for the current release sequence.

### Phase 1: Release Consistency ✅ DONE
- [x] Version bump to 0.1.5 across all projects
- [x] MIT LICENSE file
- [x] Static CI badge (no more red "failing" in hero)
- [x] --faint contrast fix (#9a9283 → #6e6759, WCAG AA)
- [x] CHANGELOG.md
- [x] README test count correction

### Phase 3: P0 Transport Fixes 🔄 IN PROGRESS
- [ ] Packet pacing — even 10ms spacing between sends
- [ ] Wi-Fi LOW_LATENCY lock lifecycle (acquire on start, release on stop)
- [ ] Foreground service type `microphone` (Android 14+)
- [ ] Battery optimization check
- [ ] Power save warning dialog
- [ ] AudioServer restart receiver

### Phase 4: Opus Integration 🔄 IN PROGRESS
- [ ] libopus NDK build (CMake, OPUS_BUILD)
- [ ] OpusEncoder/OpusDecoder JNI wrapper
- [ ] Windows P/Invoke for libopus.dll
- [ ] Protocol v2 header (codec flags)
- [ ] Variable-length Opus payload
- [ ] PCM16 fallback negotiation
- [ ] Opus FEC (forward error correction)
- [ ] NetworkTester Opus round-trip test

### Phase 5: Adaptive Jitter Buffer 🔄 IN PROGRESS
- [ ] Percentile-based delay estimator (P95 × 1.2)
- [ ] Rate-limited adjustment (±5ms per 100ms)
- [ ] Opus PLC (packet loss concealment)
- [ ] Clock drift compensation (LSF estimator)
- [ ] Tuning dashboard (configurable percentiles and bounds)

### Measurement Baseline
- [ ] Wire-level timing instrumentation (Android)
- [ ] Per-chunk latency traces
- [ ] Jitter distribution histograms
- [ ] NetworkTester timing breakdown

**Target metrics (preliminary, pending device validation):**
| Metric | v0.1.4 | v0.1.5 |
|--------|--------|--------|
| Bandwidth | 768 kbit/s | ~64 kbit/s (12× reduction) |
| Latency | ~250-320ms | ~100-150ms (2× improvement) |
| Loss tolerance | ~1-2% | ~5-10% (5× improvement) |

---

## v0.1.6 — Cross-platform Reach & Localization (EXPANDED PLAN)

### Phase 3 — Mac receiver, mobile compatibility, and global usability

This is the expanded Phase 3 release scope. Do not tag v0.1.6 until its acceptance gates below pass. Phase 3 now includes the macOS receiver, reliable interoperability with both mobile clients, and a complete initial localization pass. Later work stays in Phases 4–7 and starts after this release scope is complete.

- [ ] Build a native macOS receiver with the core Windows receiver workflow: LAN listening, manual pairing, pairing-key security, status/diagnostics, virtual microphone selection, and clean start/stop/recovery. The initial Mac preview is PCM v1/manual pairing; QR, discovery, Opus, and production distribution remain later work unless explicitly completed and accepted.
- [ ] Keep packet framing, protocol crypto, and shared control-message models in a platform-neutral Swift package where the iOS client and macOS receiver can use the same implementation; verify compatibility against Kotlin and C# fixtures.
- [ ] Make the macOS receiver accept the supported Android and iOS client protocols. Cover PCM v1 and Android's negotiated Opus v2 path where the protocol supports it; add shared cross-language fixtures for framing, key derivation, AES-GCM fields, valid packets, tamper/wrong-key rejection, and sequence exhaustion.
- [ ] Ship a macOS Audio Server Plug-in virtual microphone path so receiver audio is selectable as a microphone by conferencing/recording apps. Require physical Mac acceptance for installation, device visibility, continuity, recovery, and latency; a CI build does not prove this gate.
- [ ] Add macOS build, unit, protocol, and packaging jobs to GitHub Actions on a hosted macOS runner, so development and release builds remain possible from Windows. CI is compile/test evidence, not physical audio-device acceptance.
- [ ] Localize every user-facing surface across Android, iOS, macOS, Windows, and the public marketing site. The initial target is 21 locales: Canadian English (en-CA), American English (en-US), Spanish, French, Brazilian Portuguese, German, Italian, Dutch, Polish, Turkish, Russian, Ukrainian, Arabic, Hindi, Indonesian, Japanese, Korean, Simplified Chinese, Traditional Chinese, Central Kurdish (Sorani, ckb), and Northern Kurdish (Kurmanji, kmr). Track the target tags in `localization/target-locales.json`.
- [ ] Introduce locale-aware string resources/catalogs and formatting, an agreed translation glossary, locale selection/fallback rules, and an update workflow that makes additional locales data-only where practical. Include pluralization, text expansion, accessibility labels, and right-to-left layout support (especially Arabic) in acceptance.
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

- [ ] Keep iOS labelled experimental until a physical iPhone-to-receiver session passes. Verify both iPhone-to-Windows and iPhone-to-macOS behavior before claiming broad iOS support.
- [ ] Build Android, Windows, iOS, and macOS from one frozen candidate commit. Require Android build/lint/unit tests, Windows build/tests, iOS simulator build, and macOS build/tests to pass. Android unit-test failures must fail CI.
- [ ] On physical iPhone and Android devices with Windows and macOS receivers on the same private LAN, verify permission grant/denial, correct and incorrect pairing keys, audible playback, repeated start/stop, backgrounding where supported, network loss/recovery, and a sustained session. Capture device/OS/app versions, logs, and packet or playback evidence. Keep every unrun scenario marked BLOCKED.
- [ ] Have an external tester with a physical Mac verify audio-device selection and the documented virtual-audio route. GitHub's macOS runner can compile and test protocol behavior but cannot prove real microphone/client playback or end-user routing.
- [ ] Have fluent reviewers check all 21 initial target locales, including both Kurdish varieties, right-to-left layout, text expansion, system-language changes, critical permission/security/error copy, privacy language, and translated setup steps. Keep any locale that has not passed review out of the supported-language list.
- [ ] Recheck the existing Android-to-Windows release gates in `BUILD-EVIDENCE.md`; do not convert its encoder, Opus round-trip, locked-screen soak, packet-pacing, or clock-drift BLOCKED items into passes without new evidence.
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
- [ ] VB-CABLE renamed to PM-LAN in docs
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
- [ ] VB-CABLE renamed to PM-LAN in docs
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
