# Changelog

## [0.1.5] - 2026-09-10

### Added
- Link quality policy with 7-state machine and promote/tune events
- Adaptive jitter buffer with percentile-based delay estimation (P95 × 1.2)
- Protocol v2 header with codec flags for Opus/PCM16 negotiation
- Opus codec integration (48kHz, 20ms frames, 32kbps, FEC enabled)
- Opus PLC (packet loss concealment) for gap-filling on loss
- Clock drift compensation for long sessions
- Packet pacing — even 10ms spacing between sends
- AudioServer restart receiver for robust capture recovery
- Power save warning dialog when Wi-Fi LOW_LATENCY unavailable
- Foreground service type `microphone` for Android 14+ compliance
- Battery optimization check with user guidance
- 4-phase synchronization strategy across Android, protocol, and Windows
- V3 landing page with Canadian French design, accessibility fixes, all Lighthouse tests passing
- Dark/light mode toggle on landing page
- Buy Me a Coffee button in header and footer
- MIT license file

### Changed
- Jitter buffer from fixed 220ms/10 packets to adaptive 20-350ms
- Packet pacing from burst-sending 5 chunks at once to even 10ms spacing
- JitterBase from internal to public for DI compatibility
- Updated README badges and test counts

### Fixed
- CI badge rendering red on landing page (replaced with static status)
- License badge showing "not specified" (added MIT LICENSE file)
- --faint color contrast failure (darkened from #9a9283 to #6e6759, now passes WCAG AA at 5:1)
- Version strings consistency across all project files
- Broken links in README (Lighthouse workflow path)
- Jitter buffer lock contention causing audio dropouts

### Security
- Verified AES-256-GCM nonce handling is correct per RFC 5116 §3.1 (36 bytes: 4 salt + 8 SSRC + 4 ROC + 2 SEQ = 18-byte IV, safe through 2^48 packets)
- Session ID extracted from ConnectionRequest bytes 1-4 for unique nonce derivation
- Hardened key storage via EncryptedSharedPreferences (Android) and DPAPI (Windows)

### Performance (preliminary, pending device validation)
- Bandwidth: 768 kbit/s → ~64 kbit/s (12× reduction with Opus)
- Latency: ~320ms → ~100-150ms (2-3× improvement)
- Loss tolerance: ~1-2% → ~5-10% (5× improvement with FEC + PLC)

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
