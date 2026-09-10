# PocketMic LAN — Research Synthesis & Implementation Plan

> Generated 2026-09-10 from 5 research passes: GLM 5.3 Flash, GLM 5.3, GPT 6 Astro Pro, DeepSeek Pro (code review), and GPT meta-review. This document synthesizes all findings, resolves conflicts, and produces the definitive action plan.

---

## Key Takeaway

**The next step is a surgical release-consistency patch, not a speculative rewrite.** DeepSeek's code inspection confirms the existing engine is excellent — clean crypto, allocation-free hot paths, disciplined threading. The research reports identify future improvements, not current defects. Fix the release surface, establish measurements, then iterate.

---

## 1. What the Research Agreed On

All five passes converged on these priorities:

| # | Recommendation | Confidence | Source |
|---|----------------|------------|--------|
| 1 | **Adopt Opus** — 768→~64 kbit/s, gains FEC + PLC | High | All 5 |
| 2 | **Adaptive jitter buffer** — percentile-based, NetEQ-inspired | High | All 5 |
| 3 | **Packet pacing** — even 10ms spacing, not burst-send | High | GLM, GPT, GPT meta |
| 4 | **`WIFI_MODE_FULL_LOW_LATENCY`** — disable Wi-Fi power save | High | All 5 |
| 5 | **Oboe/AAudio** — MMAP exclusive for lower capture latency | Medium | GLM, GPT |
| 6 | **USB transport** — `adb reverse` TCP as secondary path | Medium | All 5 |
| 7 | **Foreground service `microphone` type** — required for background capture | High | GLM, GPT |
| 8 | **Clock drift handling** — needed for long sessions | High | GLM, GPT, GPT meta |

---

## 2. What Was Corrected

### The nonce-reuse bug is NOT confirmed as current

The original ROADMAP flagged a `pathId` nonce-reuse issue. DeepSeek's code inspection found:
- `sequence == -1` guard fires at 2^32 packets (~497 days)
- Reconnect preserves session ID and monotonic counter (no reset)
- Control channel uses HMAC-SHA256 with domain-separated key
- Constant-time comparison in both languages

**Action:** Reconcile the old ROADMAP item with the current code. Do NOT rewrite crypto based on the research reports. The existing construction (8-byte session ID + 4-byte sequence = 96-bit nonce) follows RFC 5116's recommended pattern.

### AES-GCM-SIV is defense-in-depth, not a requirement

GPT correctly noted: "GCM-SIV makes nonce reuse non-catastrophic, at the cost of an extra encryption pass — typically 20-40% slower." For a LAN-only app with proper session management, AES-GCM is the right primitive. GCM-SIV is a P2 hardening item.

### The nonce construction proposed by GLM 5.3 was dangerous

GPT identified: "XOR, truncate, or hash are NOT equivalent. Taking the first 12 bytes discards the counter completely." The existing construction is simpler and correct.

### Wi-Fi lock limitations are real

Both GPT passes confirmed: `WIFI_MODE_FULL_LOW_LATENCY` requires screen-on + foreground app. `WIFI_MODE_FULL_HIGH_PERF` is deprecated on API 34+ and maps to low-latency with its restrictions. Screen-off operation needs its own acceptance test.

### USB transport is viable but not zero-effort

`adb reverse` forwards TCP only (no UDP). TCP over USB bulk is ~1-3ms RTT. Requires USB debugging enabled — fine for target users (enthusiasts), not for casual users. Make it opt-in.

---

## 3. What DeepSeek Found in the Actual Code

### Strengths (confirmed, preserve)
- AES-256-GCM with correct nonce lifecycle
- Reconnect preserves crypto session (no key/nonce reuse)
- Lock-free SPSC ring for analytics
- Decoupled capture/send with DROP_OLDEST queue
- Run-generation guards prevent stale callbacks
- EncryptedSharedPreferences with plaintext→encrypted migration
- 100/100 Windows tests passing

### Release Surface Issues (fix now)

| # | Issue | Severity | Fix |
|---|-------|----------|-----|
| 1 | Version constants still say 0.1.4 (gradle, csproj, scripts) | Release blocker | Bump to 0.1.5 |
| 2 | Homepage download links hardcode v0.1.4 APK name | Delivery bug | Update or parameterize |
| 3 | Roadmap page has 5 dead internal links | Doc defect | Fix paths |
| 4 | SOURCE_SHA256SUMS.txt is stale | Verification blocker | Regenerate |
| 5 | verify_source.py exclusion mismatch | Verification bug | Fix .gitignore alignment |
| 6 | README/CHANGELOG say "74 tests" but suite passes 100 | Doc drift | Update |
| 7 | verify_protocol.py AES round-trip skipped (missing dep) | Verification gap | Install cryptography pkg |

---

## 4. Prioritized Action Plan

### Phase 1: Release Consistency Patch (1-2 days)

**Goal:** Make v0.1.5 verifiable and consistent. No feature changes.

- [ ] Bump version constants: gradle (versionCode 5, versionName "0.1.5"), csproj (Version 0.1.5), build scripts
- [ ] Update homepage download links to not hardcode version
- [ ] Fix roadmap dead links (5 links point to paths that don't exist in web/)
- [ ] Update README/CHANGELOG test count: 74 → 100
- [ ] Fix verify_source.py: align exclusions with .gitignore
- [ ] Regenerate SOURCE_SHA256SUMS.txt after all fixes
- [ ] Install `cryptography` Python package so verify_protocol.py AES round-trip actually runs
- [ ] Run all verification scripts and record results
- [ ] Tag v0.1.5, create GitHub release with artifacts

### Phase 2: Measurement Baseline (2-3 days)

**Goal:** Establish real numbers before changing anything.

- [ ] Add timing instrumentation to capture path: capture delivery time, encode duration, queue-to-send delay
- [ ] Add timing instrumentation to receive path: arrival-to-playout delay
- [ ] Record traces on OnePlus 9 Pro over Wi-Fi (healthy LAN, with loss, with interference)
- [ ] Measure actual capture latency with current AudioRecord path
- [ ] Measure battery drain: screen-on streaming, screen-off streaming
- [ ] Document the 250ms jitter tail: how often, where it originates, how quickly playback recovers
- [ ] Compare interarrival distributions: 5 GHz vs 2.4 GHz, with/without LOW_LATENCY lock

### Phase 3: P0 Transport Fixes (1 week)

**Goal:** Cheapest latency wins.

- [ ] **Packet pacing** — even 10ms spacing instead of burst-on-callback
- [ ] **`WIFI_MODE_FULL_LOW_LATENCY`** — acquire on stream start, release on stop
- [ ] **UDP buffer tuning** — 256KB send / 256KB receive on both ends
- [ ] **Foreground service `microphone` type** — required for Android 14+ background capture
- [ ] **Battery optimization exemption** — request user opt-in for sustained streaming
- [ ] Re-measure jitter tail after these changes

### Phase 4: Opus Integration (1-2 weeks)

**Goal:** 768→~64 kbit/s bandwidth, gain FEC + PLC.

- [ ] **Android:** libopus via NDK, JNI wrapper, 10ms VOIP mode, 32-48 kbit/s, FEC on, DTX off initially
- [ ] **Windows:** libopus P/Invoke (not Concentus — 5-10x faster)
- [ ] **Protocol v2 header** — codec flags, encoded payload length
- [ ] **Encoder config:** OPUS_APPLICATION_VOIP, complexity 5, VBR, in-band FEC
- [ ] **Test profiles:** 10ms vs 20ms frame, 24/32/48/64 kbit/s bitrate
- [ ] Keep PCM16 as a fallback mode for USB transport
- [ ] Re-measure: bandwidth, latency, quality under loss

### Phase 5: Adaptive Jitter Buffer (1-2 weeks)

**Goal:** Replace fixed 100/220ms with adaptive 30-120ms.

- [ ] **Percentile-based delay estimator** — EWMA of interarrival, target = P95 × 1.2
- [ ] **Rate-limited adjustment** — ±5ms per 100ms window, never step
- [ ] **Opus decoder PLC** — `opus_decode(NULL)` for lost frames
- [ ] **PCM16 fallback PLC** — waveform substitution with crossfade
- [ ] **Clock drift compensation** — sample-rate adjustment via time-stretching
- [ ] **Concealment policy** — bounded decay-repeat, then comfort noise
- [ ] Re-measure: latency, dropouts, recovery time

### Phase 6: Oboe Capture (1 week)

**Goal:** Reduce capture-side latency from ~35-50ms to ~10-20ms.

- [ ] **Oboe via NDK** — MMAP exclusive, LOW_LATENCY performance mode
- [ ] **UNPROCESSED source** — probe availability, fallback to VOICE_COMMUNICATION
- [ ] **Lock-free ring buffer** — SPSC, preallocated, no allocation in callback
- [ ] **Fallback path** — keep AudioRecord as selectable backend
- [ ] **Verify on OnePlus 9 Pro** — `AAudioStream_isMMapUsed()` confirms exclusive mode
- [ ] Re-measure: capture latency, total end-to-end

### Phase 7: USB Transport (3-5 days)

**Goal:** Sub-30ms mic-to-PC path for advanced users.

- [ ] **`adb reverse tcp:<port> tcp:<port>`** — auto-established on USB connect
- [ ] **Same packet framing** — identical AES-GCM records, TCP length-prefixed
- [ ] **Heartbeat** — 100ms keepalive probes, 500ms detection target
- [ ] **Wi-Fi failover** — new session ID on transport switch, seamless audio
- [ ] **Opt-in UI toggle** — "USB mode (requires USB debugging)"
- [ ] Re-measure: latency, failover speed

### Phase 8: DSP & Polish (P2, future)

- [ ] RNNoise via NDK (opt-in noise suppression)
- [ ] VOICE_COMMUNICATION / UNPROCESSED / Custom source selector
- [ ] AES-GCM-SIV migration (defense-in-depth)
- [ ] Windows driver evaluation (VirtualDrivers fork vs custom SysVAD)
- [ ] EncryptedSharedPreferences → Android Keystore direct migration

---

## 5. Expected Outcomes

| Metric | Current | After Phase 3 | After Phase 5 | After Phase 6 |
|--------|---------|---------------|---------------|---------------|
| Bandwidth | 768 kbit/s | 768 kbit/s | 768→64 kbit/s (Opus) | 64 kbit/s |
| End-to-end latency | ~250-320ms | ~150-200ms | ~80-120ms | ~60-80ms |
| Jitter tail (p99) | ~47ms | ~25-30ms | ~20ms | ~15-20ms |
| Packet loss tolerance | 1-2% | 1-2% | 5-10% (FEC) | 5-10% |
| Battery drain (screen-off) | Unknown | ~10-15%/hr | ~8-12%/hr | ~6-10%/hr |

These are estimates based on research, not promises. Every number must be measured.

---

## 6. What NOT to Do

1. **Don't rewrite the crypto** — the existing construction is correct per RFC 5116
2. **Don't adopt AES-GCM-SIV yet** — defense-in-depth, not a requirement
3. **Don't bundle a custom Windows driver** — VB-CABLE dependency is acceptable for now
4. **Don't promise specific latency numbers** — "60-80ms" is a target, not a guarantee
5. **Don't ship Opus without PCM16 fallback** — keep both for USB and debugging
6. **Don't force screen-on for streaming** — handle screen-off gracefully with adaptive buffer
7. **Don't add RNNoise before the base DSP chain is proven** — Tier 1 first, Tier 2 later
8. **Don't release v0.1.5 without fixing the version constants** — the DeepSeek review caught this

---

## 7. Research References (Consolidated)

### Encryption & Protocol
- RFC 7714 — AES-GCM in SRTP
- RFC 5116 — AEAD interface (nonce construction)
- RFC 8452 — AES-GCM-SIV
- RFC 3711 — SRTP replay protection
- NIST SP 800-38D — GCM specification

### Audio Codec
- RFC 6716 — Opus codec definition
- Opus API docs — encoder controls, FEC, DTX, lookahead
- Mozilla FEC experiments — in-band FEC quality under loss

### Android
- Android Oboe/AAudio docs — MMAP exclusive, low-latency mode
- Android Wi-Fi LOW_LATENCY docs — power save, scanning restrictions
- Android Foreground Service docs — microphone type requirement
- Android Keystore docs — key at rest

### Windows
- Microsoft ACX/WDM docs — kernel audio drivers
- VirtualDrivers/Virtual-Audio-Driver — render→capture loopback
- VB-Audio documentation — virtual cable behavior
- Microsoft DPAPI docs — Windows secret storage

### Jitter & Transport
- WebRTC NetEQ — adaptive jitter buffer design
- Wi-Fi 6 OFDMA paper (ACM SIGMETRICS 2023) — latency under contention
- Adaptive jitter buffer papers — percentile, GARCH, Kalman approaches

---

## 8. Decision Log

| Decision | Rationale | Source |
|----------|-----------|--------|
| Keep AES-256-GCM, don't switch to GCM-SIV | Correct construction per RFC 5116, LAN-only scope | GPT meta-review, DeepSeek |
| libopus NDK on Android, not MediaCodec | Explicit controls, consistent behavior, no OEM variance | GLM 5.3, GPT 6 |
| libopus P/Invoke on Windows, not Concentus | 5-10x faster, tracks upstream | GLM 5.3 |
| Percentile-based jitter buffer, not GARCH | Simpler, battle-tested (NetEQ), no training data needed | GLM 5.3, GPT 6 |
| USB transport opt-in, not default | Requires USB debugging, not suitable for casual users | GPT meta-review |
| Keep VB-CABLE for now | Driver work deferred until EV cert is funded | All sources |
| Don't rewrite crypto | DeepSeek found existing construction correct | DeepSeek, GPT meta-review |
| Fix release surface first | DeepSeek found version/link/verification issues | DeepSeek, GPT meta-review |
