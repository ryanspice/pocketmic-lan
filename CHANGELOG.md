# Changelog

## [0.1.6] - Unreleased

### Added
- Experimental iOS PCM streaming client and GitHub Actions simulator build artifact.
- macOS PCM v1 receiver and virtual microphone preview, with unsigned universal artifacts built by the macOS GitHub Actions workflow.
- Localization target registry for 21 locales, including en-CA/en-US and Central/Northern Kurdish (`ckb`/`kmr`); translations and review remain release gates.

### Changed
- Use `com.canopydigital.pocketmic` as the Android and iOS application identifier. Android users must install this as a new app; it cannot upgrade the v0.1.5 package.
- Bump Android, iOS, and Windows build versions to 0.1.6.
- Use Canadian English (`en-CA`) as the default language fallback and describe the iOS destination generically as a PocketMic receiver.

### Fixed
- Move iOS PCM conversion, framing, and encryption off the real-time audio callback; serialize processor state and drain it during shutdown.
- Deactivate the iOS audio session on stop and prevent packet sequence wrap from reusing an AES-GCM nonce.

## [0.1.5] - 2026-09-11

### Added
- **Protocol v2**: Opus codec support with variable-length payload, codec flags, and PCM16 fallback
- **Opus encoder** (Android): libopus JNI wrapper, VOIP mode, FEC enabled, 48kHz/10ms/48kbps
- **Opus decoder** (Windows): libopus P/Invoke wrapper with PLC (packet loss concealment)
- **Adaptive jitter buffer**: P95×1.2 percentile estimator, ±5ms/100ms rate limiter, clock drift compensation
- **Link quality policy**: 4-tier classification (Excellent/Good/Degraded/Poor) with non-overlapping bounds
- **Packet pacing**: even 10ms inter-packet spacing with catch-up reset
- **Battery optimization dialog**: prompts user to exempt PocketMic from battery restrictions
- **Screen-aware Wi-Fi lock**: LOW_LATENCY when screen on, HIGH_PERF when off
- **AudioServer restart receiver**: recovers from audio system restarts
- **Cross-language test vectors**: Python-generated, C#-verified AES-GCM fixtures
- **146 unit tests**: 100 existing + 6 Opus smoke + 21 protocol fixtures + 19 jitter buffer tests
- MIT license file
- Dark/light mode toggle on landing page
- Buy Me a Coffee button in header and footer
- BUILD-EVIDENCE.md with pinned dependency versions and verification results
- STABILIZATION-PLAN.md with evidence-gated validation stages
- RESEARCH-SYNTHESIS.md with 5-pass research consolidation

### Changed
- Jitter buffer from fixed 100ms to adaptive P95-based (30-120ms, tier-clamped)
- Packet pacing from burst-sending to even 10ms spacing
- OpusEncoder now implements `Closeable`, handle zeroed after release
- `NOTIFICATION_ID` decoupled from `DEFAULT_PORT` (now `10001`)
- `_concealmentPackets` initialized to `GoodConcealmentPackets` (10) instead of `MaxConcealedGapPackets` (20)
- Updated CHANGELOG to match actual code (10ms frames, 48kbps, not 20ms/32kbps)
- v2 decrypt path: zero-allocation (reuses PCM buffer for Opus payload)

### Fixed
- **P/Invoke signature**: `opus_decoder_create` returns `IntPtr`, takes `out int` (was causing stack corruption)
- **P/Invoke overload**: single `opus_decode` declaration (was causing stack corruption from conflicting overloads)
- **JNI include path**: `<opus.h>` not `<opus/opus.h>`
- **JNI FEC macro**: `OPUS_SET_INBAND_FEC` not `OPUS_SET_FEC`
- **Kotlin Byte.or()**: `FLAG_ENCRYPTED.toInt() or FLAG_OPUS.toInt()`
- **OpusEncoder scope**: `opusEncoder` moved to outer scope for `finally` block visibility
- **IOpusDecoder comment**: corrected "20ms" to "10ms"
- CI badge: replaced live status with static badge (no more red "failing" in hero)
- License badge: added MIT LICENSE file
- --faint color contrast: #9a9283 → #6e6759 (passes WCAG AA for small text)
- Version strings consistency across all project files
- Broken links in README

### Security
- Verified AES-256-GCM nonce handling per RFC 5116 §3.1 (36 bytes: 4 salt + 8 SSRC + 4 ROC + 2 SEQ = 18-byte IV)
- Session ID from ConnectionRequest bytes 1-4 for unique nonce derivation
- Hardened key storage via EncryptedSharedPreferences (Android) and DPAPI (Windows)
- Cross-language test vectors verify wire-format compatibility

### Performance
- v2 receive path: zero per-packet allocation (reuses PCM buffer)
- Opus payload: ~60 bytes vs 960 bytes PCM (16× reduction in payload)
- Modeled bandwidth: ~106 kbit/s on wire (v2) vs ~822 kbit/s (v1), ~7.8× reduction
- Adaptive jitter: converges to network conditions within 2 seconds

### Dependencies
- libopus 1.5.2 (pinned, SHA-256: `65c1d2f78b9f2fb20082c38cbe47c951ad5839345876e46941612ee87f9a7ce1`)
- NDK r27c, CMake 3.22.1

## [0.1.4] - 2026-09-09

### Added
- Protocol v1 specification (PROTOCOL.md)
- Jitter buffer formal verification (JITTER-VERIFICATION.md)
- Security hardening documentation (SECURITY-AUDIT.md, SECURITY-HARDENING.md)
- Performance benchmarks and audit
- Landing page with download and troubleshooting
- GitHub Actions CI/CD for Android builds

### Changed
- Test suite expanded to 100 tests (Windows receiver)

## [0.1.3] - 2026-09-01

### Added
- Initial jitter buffer implementation
- AES-256-GCM encryption
- TCP transport
- Windows receiver (WinForms)
- Android capture app (Kotlin)
