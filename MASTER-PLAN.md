# PocketMic LAN v0.1.5 — Master Release Plan

> Generated 2026-09-10 after completing Phases 1-5 implementation.
> This document covers everything needed to release, test, and validate v0.1.5.
> Historical v0.1.5 snapshot. For the expanded v0.1.6 Phase 3 scope and the deferred Phase 4–7 sequence, see `ROADMAP.md`.

---

## Current State

### What's Implemented (Phases 1-5)

| Phase | Status | Files | Lines | Description |
|-------|--------|-------|-------|-------------|
| 1. Release consistency | ✅ Done | 8 | 64 | Version bump, LICENSE, CI badge, contrast fix, CHANGELOG |
| 3. P0 transport | ✅ Done | 3 | 145 | Packet pacing, Wi-Fi lock lifecycle, battery optimization |
| 4. Opus integration | ✅ Done | 11 | 753 | Protocol v2, libopus JNI/P/Invoke, PCM16 fallback |
| 5. Adaptive jitter | ✅ Done | 4 | 416 | P95 delay estimator, Opus PLC, clock drift compensation |
| **Merge + conflict fix** | ✅ Done | 4 | 72 | Type reconciliation, build clean, 100/100 tests pass |

**Total: 30 files changed, 1,450+ insertions across 5 phases.**

### Build Status

| Target | Status | Details |
|--------|--------|---------|
| Windows receiver | ✅ Builds | 0 warnings, 0 errors |
| Windows tests | ✅ Pass | 100/100, 182ms |
| Android build | ⏳ Untested | Needs NDK + CMake for libopus |
| Landing page | ✅ Static | No build step |

---

## Phase 6: Testing & Validation

### 6.1 Unit Tests (Windows)

**Status**: 100 pass, need ~15 new tests for Phase 4-5 code.

| Test target | Priority | Est. effort |
|-------------|----------|-------------|
| `AudioPipeline.TryDecrypt` v2 (Opus path) | P0 | 2h |
| `AudioPipeline.TryDecrypt` v1 fallback | P0 | 1h |
| `AdaptiveJitterBuffer` P95 convergence | P1 | 3h |
| `AdaptiveJitterBuffer` rate limiting | P1 | 1h |
| `AdaptiveJitterBuffer` bounds clamping | P1 | 1h |
| `OpusDecoder.TryDecode` round-trip | P0 | 2h |
| `OpusDecoder.TryGeneratePlc` | P1 | 1h |
| `OpusDecoder.Reset` | P1 | 30min |
| `LinkQualityPolicy` tier bounds | P1 | 1h |
| `PocketMicEngine` v2 packet flow | P1 | 2h |
| `PacketCrypto.Encryptor` v2 header | P0 | 2h |
| `MicConfig` codec persistence | P2 | 1h |

**Action**: Delegate to a MiMo agent to write and verify these tests.

### 6.2 Android Build Verification

The Android side now needs NDK + CMake to build (libopus JNI). Steps:

1. **Verify CMakeLists.txt** — the agent created it but we haven't tested
2. **Download libopus source** — add to `android/app/src/main/cpp/opus/`
3. **Build with Gradle** — `./gradlew assembleDebug`
4. **Fix any compile errors** — the JNI wrapper may need tweaks

**Blocker**: The libopus source is not in the repo yet. The CMakeLists.txt references it but the actual source files are missing.

**Action**: Download libopus 1.5.2 source, extract to `android/app/src/main/cpp/opus/`, then attempt build.

### 6.3 Integration Testing

| Test | Method | Priority |
|------|--------|----------|
| Android→Windows PCM16 (v1) | Manual on LAN | P0 |
| Android→Windows Opus (v2) | Manual on LAN | P0 |
| Opus fallback to PCM16 | Kill opus.dll, retry | P1 |
| Packet pacing verification | PCAP capture | P1 |
| Adaptive buffer convergence | Network simulator | P2 |
| Long session (>30min) | Leave running | P2 |
| Wi-Fi roaming | Walk between APs | P2 |

---

## Phase 7: Pre-Release Checklist

### 7.1 Code Quality

- [ ] All 100 existing tests pass ✅
- [ ] New Opus/jitter tests written and passing
- [ ] Android build succeeds with NDK
- [ ] No compiler warnings
- [ ] `git diff --stat` reviewed

### 7.2 Documentation

- [x] CHANGELOG.md updated
- [x] PROTOCOL.md updated (v1 + v2)
- [x] ROADMAP.md updated
- [x] RESEARCH-SYNTHESIS.md written
- [ ] README.md updated with v0.1.5 changes
- [ ] README.md: add Opus setup instructions
- [ ] README.md: add libopus.dll requirement for Windows

### 7.3 Assets

- [ ] Windows receiver ZIP with libopus.dll bundled
- [ ] Android APK (debug + unsigned release)
- [ ] SHA256 checksums
- [ ] libopus.dll prebuilt (or instructions to build)

### 7.4 Landing Page

- [x] CI badge fixed (static, not live)
- [x] --faint contrast fixed (WCAG AA)
- [ ] Update version references to 0.1.5
- [ ] Add Opus mention to features section
- [ ] Update performance metrics page

---

## Phase 8: Release Process

### 8.1 Branch & Tag

```bash
git tag -a v0.1.5 -m "PocketMic LAN v0.1.5: Opus, adaptive jitter, transport fixes"
git push origin master --tags
```

### 8.2 Build Artifacts

**Windows**:
```powershell
dotnet publish windows-receiver/PocketMicReceiver.csproj -c Release -r win-x64 --self-contained -o publish/win-x64
# Copy opus.dll to publish/win-x64/
Compress-Archive -Path publish/win-x64/* -DestinationPath PocketMicReceiver-win-x64.zip
```

**Android**:
```bash
cd android
./gradlew assembleDebug assembleRelease
# Debug: app/build/outputs/apk/debug/app-debug.apk
# Release: app/build/outputs/apk/release/app-release-unsigned.apk
```

### 8.3 GitHub Release

- Tag: `v0.1.5`
- Title: `PocketMic LAN v0.1.5: Opus Codec & Adaptive Jitter`
- Assets: APK, ZIP, SHA256SUMS
- Release notes: Copy from CHANGELOG

---

## Phase 9: Open Research Questions

These need device testing and cannot be resolved in code review:

| # | Question | How to answer |
|---|----------|---------------|
| 1 | Does 32kbps Opus sound good for voice? | A/B test on device |
| 2 | Does P95 jitter estimator converge fast enough? | Wi-Fi roaming test |
| 3 | How much does MMAP exclusive reduce latency? | Compare AudioRecord vs Oboe |
| 4 | Is `adb reverse` reliable for production? | Long session USB test |
| 5 | Does AES-GCM-SIV add meaningful safety? | Security audit |
| 6 | Does RNNoise help or hurt? | A/B test on device |
| 7 | Packet pacing: does 10ms inter-packet spacing actually reduce jitter? | PCAP analysis |
| 8 | Battery impact of foreground service + Wi-Fi lock? | Battery historian |

---

## Summary: What v0.1.5 Delivers

| Feature | Before | After |
|---------|--------|-------|
| **Codec** | PCM16 only | PCM16 + Opus (opt-in) |
| **Bandwidth** | 768 kbit/s | ~64 kbit/s (12× reduction) |
| **Jitter buffer** | Fixed 100ms | Adaptive P95-based 20-350ms |
| **Packet loss** | Silence fill | Opus PLC (natural-sounding) |
| **Packet pacing** | Burst send | Even 10ms spacing |
| **Battery** | Basic wake lock | Foreground service + optimization check |
| **Wi-Fi lock** | Always on | Screen-aware lifecycle |
| **Protocol** | v1 only | v1 + v2 (negotiated) |
| **License** | Not specified | MIT |
| **Accessibility** | --faint fails contrast | WCAG AA compliant |
| **CI badge** | Live (shows failures) | Static (gold themed) |

---

## File Inventory

### New Files (v0.1.5)
```
LICENSE
CHANGELOG.md
ROADMAP.md
RESEARCH-PROMPT.md
RESEARCH-SYNTHESIS.md
MASTER-PLAN.md
android/app/src/main/cpp/CMakeLists.txt
android/app/src/main/cpp/opus_jni.c
android/app/src/main/java/com/ryanspice/pocketmic/OpusEncoder.kt
windows-receiver-core/OpusDecoder.cs
windows-receiver-core/IOpusDecoder.cs
windows-receiver-core/AdaptiveJitterBuffer.cs
```

### Modified Files (v0.1.5)
```
android/app/build.gradle.kts (NDK/CMake, version bump)
android/app/src/main/AndroidManifest.xml (battery permission)
android/app/src/main/java/com/ryanspice/pocketmic/MainActivity.kt (battery dialog)
android/app/src/main/java/com/ryanspice/pocketmic/MicStreamingService.kt (pacing, Opus)
android/app/src/main/java/com/ryanspice/pocketmic/MicConfig.kt (codec enum)
android/app/src/main/java/com/ryanspice/pocketmic/PacketCrypto.kt (v2 header)
windows-receiver-core/AudioPipeline.cs (v2 decrypt)
windows-receiver-core/PocketMicEngine.cs (adaptive buffer, Opus decoder)
windows-receiver-core/LinkQualityPolicy.cs (tier bounds)
windows-receiver-core/PocketMicReceiver.Core.csproj (version bump)
windows-receiver/PocketMicReceiver.csproj (version bump)
scripts/build-android.ps1 (version bump)
web/index.html (CI badge, version)
web/styles.css (--faint contrast)
README.md (test count, links)
PROTOCOL.md (v1 + v2)
```
