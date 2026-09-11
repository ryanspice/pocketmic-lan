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

### BLOCKED (needs hardware)

| Check | Status | Blocker |
|-------|--------|---------|
| Android build (NDK) | ⬜ BLOCKED | Needs Android SDK + NDK |
| Android encoder smoke test | ⬜ BLOCKED | Needs device/emulator |
| v2 with Opus decoder round-trip | ⬜ BLOCKED | Needs Opus-encoded test vector |

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
