# PocketMic LAN — Roadmap

> Last updated: 2026-09-10 | Current: v0.1.5 | Master: v1.0.0

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

## 🔨 v0.1.5 — Performance & Quality (IN PROGRESS)

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

## v0.1.6 — Polish & Usability (PLANNED)

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
