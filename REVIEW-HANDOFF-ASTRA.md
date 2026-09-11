# PocketMic LAN v0.1.5 — Review Handoff for Astra 6 Pro

> **Repo**: `B:\Dev\android\pocketmic-lan` (GitHub: `ryanspice/pocketmic-lan`)
> **Branch**: `master` at `dbb787a`
> **Date**: 2026-09-11
> **Reviewer**: Astra 6 Pro
> **Scope**: Full code review of 17 changed files (1,588 insertions, 48 deletions)

---

## Context

PocketMic LAN is an Android-to-Windows local audio streaming app. The phone captures microphone audio and sends it over UDP to a Windows receiver that plays it through a virtual audio cable (VB-CABLE). Use cases: Discord voice, OBS streaming, meetings.

**v0.1.5 adds**: Opus codec (12× bandwidth reduction), adaptive jitter buffer, packet pacing, battery optimization, protocol v2, and several accessibility/license fixes.

The code was produced by 3 parallel MiMo agents (transport, Opus, jitter buffer) plus direct work (release consistency, conflict resolution). All agents operated on isolated worktrees and were cherry-picked into master. Build is clean (0 warnings, 0 errors), 100/100 xUnit tests pass.

---

## What to Review

### Priority 1: Security & Correctness

| File | Area | What to check |
|------|------|---------------|
| `PacketCrypto.kt` | v2 header | Does the 28-byte header maintain AAD integrity? Is `payloadLength` authenticated? |
| `PacketCrypto.kt` | Encryptor | Variable-length Opus payload — does the GCM tag cover the right bytes? |
| `AudioPipeline.cs` | TryDecryptV2 | Opus decryption path — does it correctly validate header before decrypting? |
| `opus_jni.c` | JNI | Buffer overflows, null pointer handling, proper cleanup on error |
| `OpusDecoder.cs` | P/Invoke | Is the byte[] marshalling correct for opus_decode? Does PLC (null data) work? |

### Priority 2: Correctness & Edge Cases

| File | Area | What to check |
|------|------|---------------|
| `AdaptiveJitterBuffer.cs` | P95 estimator | Does the 200-sample rolling window converge? Rate limiter (±5ms/100ms) — is it too slow? |
| `AdaptiveJitterBuffer.cs` | Drift compensation | Linear regression over 5s — is this robust to burst loss? |
| `PocketMicEngine.cs` | Integration | Adaptive buffer + LinkQualityPolicy cooperation — do they fight? |
| `MicStreamingService.kt` | Packet pacing | 10ms pacing with catch-up reset — does this actually reduce jitter or just delay it? |
| `MicStreamingService.kt` | Opus fallback | If libopus fails to load mid-stream, does it cleanly fall back to PCM? |
| `OpusEncoder.kt` | Lifecycle | JNI handle leak on exception? Double-destroy safety? |

### Priority 3: Architecture & Design

| File | Area | What to check |
|------|------|---------------|
| `IOpusDecoder.cs` | Interface | Is TryDecode/TryGeneratePlc the right abstraction? Should it be async? |
| `LinkQualityPolicy.cs` | Tier bounds | Are Excellent/Good/Degraded/Poor ranges realistic? |
| `MicConfig.kt` | AudioCodec enum | PCM/OPUS — is this the right place for codec selection? Should it be in ControlProtocol? |
| `PROTOCOL.md` | v2 spec | Is the v2 datagram format unambiguous? Any edge cases in the variable-length payload? |

### Priority 4: Testing Gaps

The existing 100 tests cover v1 protocol, encryption, jitter buffer basics, and engine lifecycle. **No tests exist yet for**:

1. v2 protocol (Opus encrypt/decrypt round-trip)
2. AdaptiveJitterBuffer convergence behavior
3. OpusDecoder.TryDecode / TryGeneratePlc
4. PacketCrypto.Encryptor with Opus payload
5. LinkQualityPolicy tier bounds
6. AudioPipeline.TryDecryptV2

---

## Key Files to Read First

### Protocol (start here)
```
PROTOCOL.md                              — v1 + v2 datagram format
```

### Android side
```
android/app/src/main/java/.../PacketCrypto.kt      — v2 header, AudioCodec, Encryptor
android/app/src/main/java/.../MicStreamingService.kt — capture loop, pacing, Opus encoding
android/app/src/main/java/.../MicConfig.kt          — AudioCodec enum, persistence
android/app/src/main/java/.../OpusEncoder.kt        — JNI wrapper
android/app/src/main/cpp/opus_jni.c                  — Native encoder
android/app/src/main/cpp/CMakeLists.txt              — NDK build
```

### Windows side
```
windows-receiver-core/AudioPipeline.cs           — v1+v2 decrypt, Opus decode
windows-receiver-core/OpusDecoder.cs             — P/Invoke decoder
windows-receiver-core/IOpusDecoder.cs            — Interface
windows-receiver-core/AdaptiveJitterBuffer.cs    — P95 estimator, drift
windows-receiver-core/LinkQualityPolicy.cs       — Tier bounds
windows-receiver-core/PocketMicEngine.cs         — Integration point
```

### Planning & research
```
RESEARCH-SYNTHESIS.md   — 5-pass research synthesis with sources
MASTER-PLAN.md          — Release plan, testing gaps, open questions
ROADMAP.md              — v0.1.4 through v1.0.0
```

---

## Questions for the Reviewer

1. **Opus bitrate**: We chose 48kbps (VOIP mode). Is 32kbps sufficient for voice? What's the quality tradeoff?
2. **Adaptive buffer P95**: Is P95 × 1.2 the right multiplier? WebRTC uses P95 × 1.5 — should we match?
3. **Rate limiter**: ±5ms per 100ms — is this too conservative for Wi-Fi roaming (AP handoff takes ~200ms)?
4. **Protocol v2 AAD**: The v2 header includes `payloadLength` in the authenticated data. Is this correct? Should it be encrypted?
5. **JNI safety**: The opus_jni.c uses `GetByteArrayElements` — should we use `GetPrimitiveArrayCritical` for lower latency?
6. **Clock drift**: Linear regression over 5s — is this robust to burst loss (e.g., 50 packets lost, then recovery)?
7. **AES-GCM-SIV**: The research synthesis recommends this as defense-in-depth. Is it worth the complexity?
8. **Oboe vs AudioRecord**: Phase 6 plans Oboe MMAP exclusive. Is the latency improvement real-world significant on mid-range devices?

---

## Build & Test Instructions

### Windows
```bash
cd B:\Dev\android\pocketmic-lan
"/c/Program Files/dotnet/dotnet.exe" build windows-receiver-core/PocketMicReceiver.Core.csproj
"/c/Program Files/dotnet/dotnet.exe" test windows-receiver-tests/PocketMicReceiver.Tests.csproj
```

### Android (requires NDK)
```bash
cd B:\Dev\android\pocketmic-lan\android
# Need to download libopus source first:
# https://opus-codec.org/downloads/
# Extract to android/app/src/main/cpp/opus/
./gradlew assembleDebug
```

---

## What's NOT in Scope

- Landing page design (already reviewed, fixes applied)
- CI/CD workflow changes (separate PR)
- Documentation prose quality (already reviewed)
- Windows UI changes (no UI changes in this release)
- Play Store / signing (planned for v1.0.0)
