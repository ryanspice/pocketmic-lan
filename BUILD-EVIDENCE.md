# PocketMic LAN v0.1.5 — Build Evidence

> Stage 6A of STABILIZATION-PLAN.md
> Recorded: 2026-09-11

---

## Pinned Dependencies

### libopus 1.5.2

| Field | Value |
|-------|-------|
| Version | 1.5.2 |
| Download URL | https://downloads.xiph.org/releases/opus/opus-1.5.2.tar.gz |
| SHA-256 | `65c1d2f78b9f2fb20082c38cbe47c951ad5839345876e46941612ee87f9a7ce1` |
| License | BSD (COPYING file in source tree) |
| Why 1.5.2 not 1.6.1 | Research synthesis written against 1.5.x API; CMakeLists and JNI designed for 1.5.x; upgrading needs separate validation |
| Installed at | `android/app/src/main/cpp/opus/` |
| Setup script | `scripts/setup-libopus.ps1` |

### Android NDK / CMake

| Field | Value |
|-------|-------|
| NDK version | (record after build) |
| CMake version | (record after build) |
| Kotlin version | 2.2.21 (from build.gradle.kts) |
| AGP version | 8.13.2 (from build.gradle.kts) |
| compileSdk | 36 |
| minSdk | 26 |
| targetSdk | 36 |
| Supported ABIs | arm64-v8a, armeabi-v7a, x86_64 |

### Windows .NET

| Field | Value |
|-------|-------|
| .NET version | 8.0 (from csproj TargetFramework) |
| NAudio | (record version from csproj) |
| Architecture | win-x64 |

---

## Native Library Convention

| Platform | P/Invoke name | Resolved filename | Load mechanism |
|----------|---------------|-------------------|----------------|
| Windows | `"opus"` | `opus.dll` | .NET DllImport appends .dll |
| Android | `"opus"` | `libopus.so` | System.loadLibrary prepends lib |

---

## CMakeLists.txt

Builds libopus from source as a shared library, then links `pocketmic_jni` (opus_jni.c) against it.

**Source tree**: `android/app/src/main/cpp/opus/`
**JNI wrapper**: `android/app/src/main/cpp/opus_jni.c`

---

## Build Verification

### Windows receiver

| Check | Result | Evidence |
|-------|--------|----------|
| Build (dotnet build) | ✅ PASS | 0 warnings, 0 errors |
| Tests (100 xUnit) | ✅ PASS | 100/100, 182ms |
| opus.dll built from source | ✅ PASS | libopus 1.5.2, SHA-256: `08ee50be...`, 456704 bytes |
| Opus smoke tests (6) | ✅ PASS | 6/6: create, PLC, decode, reset, double-dispose |
| Protocol fixture tests (21) | ✅ PASS | v1/v2 decrypt, tamper, replay, nonce, header parsing |
| All tests (127 xUnit) | ✅ PASS | 127/127, 33ms |
| P/Invoke fix | ✅ PASS | opus_decoder_create returns IntPtr, takes out int |

### Stage 6B — Protocol & Codec Correctness

| Check | Result | Evidence |
|-------|--------|----------|
| Cross-language vectors | ✅ PASS | tests/fixtures/vectors.json (Python-generated, C#-verified) |
| v1 decrypt matches expected PCM | ✅ PASS | 960-byte alternating payload |
| v2 without decoder returns false | ✅ PASS | Expected: no decoder → reject |
| Tampered header rejected | ✅ PASS | Bit flip in session ID → AAD mismatch |
| Tampered ciphertext rejected | ✅ PASS | Bit flip at offset 30 → GCM failure |
| Tampered tag rejected | ✅ PASS | Bit flip in tag → GCM failure |
| Wrong key rejected | ✅ PASS | Different key → GCM failure |
| Truncated payload rejected | ✅ PASS | 500-byte packet → length check |
| Wrong version rejected | ✅ PASS | Version 3 → unsupported |
| v2 tampered payload length rejected | ✅ PASS | Bit flip at offset 24 |
| No encryption flag rejected | ✅ PASS | Flags=0 → rejected |
| Appended bytes rejected | ✅ PASS | 1010-byte packet → length check |
| Nonce includes session ID | ✅ PASS | Different session → different nonce |
| Nonce includes sequence | ✅ PASS | Different seq → different nonce |
| HeaderInfo v1/v2/invalid | ✅ PASS | Correct size and codec detection |
| Decrypt is deterministic | ✅ PASS | Same input → same output |

### Stage 6C — Runtime Behavior

| Check | Result | Evidence |
|-------|--------|----------|
| P95 steady state (10ms) | ✅ PASS | RawP95Ms ≈ 10ms after 250 packets |
| P95 moderate jitter (10/15ms) | ✅ PASS | RawP95Ms ≈ 15ms |
| P95 single outage (510ms gap) | ✅ PASS | Gap rejected (>500ms), P95 unchanged |
| P95 short gap (200ms) | ✅ PASS | One gap doesn't raise 95th percentile |
| Rate limiter caps growth | ✅ PASS | Target stays in [30, 120] with sudden jitter |
| Target clamped to min floor | ✅ PASS | Low jitter → target converges to 30ms |
| Target clamped to max ceiling | ✅ PASS | High jitter → target reaches 120ms |
| Tier bounds: Excellent [40,60] | ✅ PASS | |
| Tier bounds: Good [60,120] | ✅ PASS | |
| Tier bounds: Degraded [120,200] | ✅ PASS | |
| Tier bounds: Poor [200,300] | ✅ PASS | |
| Tier boundaries non-overlapping | ✅ PASS | Excellent.Max == Good.Min, etc. |
| Effective prebuffer clamped to tier | ✅ PASS | Target 30ms → clamped to 60ms in Good tier |
| Effective prebuffer follows target | ✅ PASS | Within tier range |
| Reset clears all state | ✅ PASS | RawP95=0, drift=0, target=default |
| Drift rate zero before warmup | ✅ PASS | <20 packets → drift=0 |
| Drift rate zero before 2s | ✅ PASS | <2s wall-clock → drift=0 |
| Drift warmup packets skipped | ✅ PASS | <20 packets → drift=0 |
| Default prebuffer positive | ✅ PASS | |

### BLOCKED (needs hardware)

| Check | Status | Blocker |
|-------|--------|---------|
| Android build (NDK) | ✅ PASS | assembleDebug SUCCESS, 30s |
| APK: libopus.so (arm64-v8a) | ✅ PASS | apkanalyzer confirms |
| APK: libopus.so (armeabi-v7a) | ✅ PASS | apkanalyzer confirms |
| APK: libopus.so (x86_64) | ✅ PASS | apkanalyzer confirms |
| APK: libpocketmic_jni.so (all ABIs) | ✅ PASS | apkanalyzer confirms |
| Install on OnePlus 9 Pro (Android 14) | ✅ PASS | adb install Success |
| Android encoder smoke test | ⬜ BLOCKED | Needs app launch + logcat |
| v2 with Opus decoder round-trip | ⬜ BLOCKED | Needs end-to-end test |
| 30-min locked-screen soak | ⬜ BLOCKED | Needs real-time test |
| Packet pacing PCAP trace | ⬜ BLOCKED | Needs real network |
| Drift convergence (real clock) | ⬜ BLOCKED | Needs 2+ seconds real-time test |

---

## Signing

| Artifact | Signing | Certificate fingerprint |
|----------|---------|------------------------|
| Debug APK | Debug keystore | (record after build) |
| Release APK | Release key | ⬜ Needs key generation |

---

## Upstream Licenses

Opus BSD license included at: `android/app/src/main/cpp/opus/COPYING`

Must be bundled in:
- [ ] Windows ZIP
- [ ] Android APK (assets or about screen)
