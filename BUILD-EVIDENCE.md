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
| Build (dotnet build) | ✅ PASS | 0 warnings, 0 errors, 4.5s |
| Tests (100 xUnit) | ✅ PASS | 100/100, 182ms |
| Publish (self-contained ZIP) | ⬜ TODO | |
| Decoder smoke test | ⬜ BLOCKED | Need opus.dll built from pinned 1.5.2 source |
| `opus.dll` in package | ⬜ BLOCKED | Depends on above |

### Android APK

| Check | Result | Evidence |
|-------|--------|----------|
| Build (gradlew assembleDebug) | ⬜ TODO | |
| APK contains libopus.so (arm64-v8a) | ⬜ TODO | |
| APK contains libopus.so (armeabi-v7a) | ⬜ TODO | |
| APK contains libopus.so (x86_64) | ⬜ TODO | |
| Encoder smoke test (device/emulator) | ⬜ BLOCKED | Needs Android device or emulator |
| Install + start + connect | ⬜ BLOCKED | Needs Android device + Windows receiver |

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
