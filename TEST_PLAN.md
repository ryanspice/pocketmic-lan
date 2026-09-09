# PocketMic LAN — Comprehensive Test Plan

> Generated 2026-09-09 by codebase audit. Covers Android (Kotlin) and Windows (C#) sides.

---

## 1. COVERAGE MAP

### Android (Kotlin)

| Source File | Test File | What IS Tested | What IS NOT Tested |
|---|---|---|---|
| **PacketCrypto.kt** | PacketCryptoTest.kt | Encryptor encrypt/decrypt roundtrip, buffer reuse, compatibility helper | `deriveKey` determinism, `buildHeader` field layout, zero-payload rejection, `readIntBigEndian` boundary, `writeShortBigEndian`/`writeLongBigEndian` correctness |
| **ControlProtocol.kt** | ControlProtocolTest.kt | Build/parse roundtrip, HMAC verify/reject, magic/version rejection, announce payload roundtrip (nonce, port, name, versions, trailer), version peek, stats decode, config payload encode/decode, encodeCapped (no-split, char/byte caps) | Empty payload edge, zero-length announce name, config payload at exact quantization boundaries, `Announce.audioProtocolMatches` with various protocol versions, `Announce.frontEndName` exhaustive mapping |
| **ControlChannel.kt** | ControlChannelTest.kt | ProbeNonceRing freshness: current, previous round, old replay, never-sent, wrong-size, wrong-size remember | `broadcastFor` subnet computation, `sameSubnet` prefix logic, `reportIfVersionMismatch`, `broadcastTargets` with null network, stale announce counting, `sendConfig` fire-and-forget |
| **ReconnectPolicy.kt** | ReconnectPolicyTest.kt | `evaluate()`: healthy link, grace period, silence→reconnect, timeout, recovery, give-up, last-pass recovery; `isDifferentTarget`: same, moved, incomplete, port boundary; AES-GCM nonce non-reuse across reconnects and new sessions | Boundary exactly at STATS_TIMEOUT_MS, reconnect duration tracking, multiple rapid reconnect cycles |
| **MicConfig.kt** | MicConfigTest.kt | `generatePairingKey()`: length, alphabet, non-determinism; `validateConnection()`: blank host, short key, port range, highest port | `CaptureMode.fromWireValue()` for all modes + fallback, `DspSettings` defaults, `FabCorner.fromName()` fallback, `AppPrefs` load/save (Android-dependent) |
| **SendQueueTest.kt** | *(direct)* | Order delivery, drop-oldest eviction, undelivered hook counting, non-blocking producer | Concurrent produce/consume race behavior, queue close during active send, zero-capacity edge |
| **MicStreamingService.kt** | — | *(No tests)* | Audio source candidate fallback chain, gain scaling, sequence exhaustion, reconnect redirect, Wi-Fi lock policy logic, notification creation, `audioSourceName()` mapping |
| **StreamingState.kt** | — | *(No tests)* | `update()` transform, `reset()` clears state |
| **MainActivity.kt** | — | *(No tests)* | Compose UI (typically instrumented tests), `attemptStart()` validation, permission flow |
| **QrScanner.kt** | — | *(No tests)* | `parseQrPairingData()`: valid URI, pmic:// prefix, missing host, short key, port boundary, default port fallback |
| **DiagnosticsScreen.kt** | — | *(No tests)* | Compose rendering (instrumented) |
| **DiagnosticsLog.kt** | — | *(No tests)* | `ensureStarted()` idempotency, `append()` capacity cap, state transition logging |

### Windows (C#)

| Source File | Test File | What IS Tested | What IS NOT Tested |
|---|---|---|---|
| **AudioPipeline.cs** | AudioPipelineTests.cs | TryDecrypt: well-formed, wrong length, missing magic, future version, encrypted flag, header length, wrong key, tampered ciphertext; PeekProtocolVersion; SequenceDelta (zero, gap, late, rollover); BuildConcealmentFrame (no previous, reduced level, monotonically fade, silence at max, past max); SmoothedRms; ShouldTrim (loud, hard ceiling, quiet, at boundary, exact threshold); HighWaterFor | Jitter buffer insertion ordering, `DefaultPrebufferMilliseconds`/`DefaultHighWaterMilliseconds` correctness |
| **ControlProtocol.cs** | ControlProtocolTests.cs | Build/parse roundtrip, HMAC verify/reject, magic/version rejection, announce trailer position, version trailer survival, legacy announce, config decode (short, exact, trailing bytes, quantization, max), stats payload, peek version | `EncodeCapped` (multi-byte truncation), empty announce name, zero payload, stats decode from Android |
| **VoiceProcessor.cs** | — | *(No tests)* | DC blocker, high-pass filter, noise gate (hysteresis, floor tracking), compressor, limiter, presence EQ, `Apply()` redesign trigger, `DenoiseHook` invocation, `GateActive` state |
| **PocketMicEngine.cs** | — | *(No tests)* | Start/stop lifecycle, control channel probe reply, stats loop, silence detection (CheckForSilence), `TryDecryptPacket` integration, monitor output toggle, prebuffer setting |
| **ReceiverSettings.cs** | — | *(No tests)* | JSON serialization roundtrip, corrupt file recovery, default values |
| **SingleInstance.cs** | — | *(No tests)* | Mutex acquire/release, fallback namespace, `FindOthers`, `CloseOthersAsync` |
| **DefaultDeviceSwitcher.cs** | — | *(No tests)* | Device discovery, IPolicyConfig COM interop (platform-specific, manual QA) |
| **Analytics/*.cs** | AnalyticsTests.cs | Digital silence NaN handling, level averaging, gate percentage, connection tracker (attempts, silence episodes, reconnects, version mismatches), session flush, build version | Window aggregation math (10s/1min/session), packet sample queue, session recorder JSONL output, `FlushFinal` partial windows |

---

## 2. MISSING TESTS BY PRIORITY

### P0 — Security / Crypto / Protocol Correctness

| # | Gap | Why it matters |
|---|---|---|
| P0-1 | `parseQrPairingData()` — no tests at all | QR code is the primary onboarding path; a parsing bug silently sends audio to the wrong host |
| P0-2 | Cross-platform control frame interop | Android builds frames, Windows parses them. No test that a Kotlin-built frame actually decodes in C# and vice versa |
| P0-3 | `Announce.audioProtocolMatches` correctness when `hasVersions=true` but version differs | Key protocol negotiation edge case |
| P0-4 | `announcePayload` with empty host name (`""`) | Edge case on discovery |
| P0-5 | AES-GCM nonce uniqueness under concurrent encrypt calls | The Encryptor reuses buffers; a race would be catastrophic |
| P0-6 | `deriveKey()` determinism for same input, divergence for different input | Key derivation correctness |
| P0-7 | `ControlProtocol.verify()` constant-time comparison actually rejects timing attacks | Security property of HMAC verification |
| P0-8 | `readAnnounce()` rejects truncated payloads | Security: malformed frames must not produce partial state |
| P0-9 | `readStats()` rejects payloads shorter than 45 bytes | Already tested on Android; verify C# side |
| P0-10 | `TryReadConfig()` rejects payloads shorter than 7 bytes | Already tested on C#; verify Android has no raw config reader |

### P1 — Core Audio Pipeline

| # | Gap | Why it matters |
|---|---|---|
| P1-1 | VoiceProcessor `Process()` DSP chain — no tests | The entire receiver audio quality depends on correct gate/compressor/limiter/HPF behavior |
| P1-2 | VoiceProcessor `Apply()` — redesign only on parameter change | Performance: unnecessary filter recalculation |
| P1-3 | VoiceProcessor DC blocker convergence on offset signal | DC offset eats headroom before limiter |
| P1-4 | `SmoothedRms` asymmetric attack/release behavior | Trimming depends on correct level tracking |
| P1-5 | `ShouldTrim` hard ceiling vs quiet gating | Trim behavior during mixed loud/quiet passages |
| P1-6 | PacketCrypto `Encryptor` with `pcmBytes=0` rejection | `require(pcmBytes > 0)` edge case |
| P1-7 | Sequence rollover (uint32 wrap) at `uint.MaxValue` on Android encryptor | Long streaming sessions will hit this |
| P1-8 | `StreamingState.update()` and `reset()` behavior | State management correctness |
| P1-9 | `CaptureMode.fromWireValue()` fallback to `CLEAN` for unknown values | Unknown mode from receiver |
| P1-10 | Windows `AudioPipeline.TryDecrypt` with wrong sample rate | Receiver rejects 44.1kHz packets |

### P2 — UI / Integration / Edge Cases

| # | Gap | Why it matters |
|---|---|---|
| P2-1 | `MicStreamingService.audioSourceCandidates()` returns correct source list per mode | Critical for audio quality on different phones |
| P2-2 | `MicStreamingService.audioReadError()` returns correct messages per error code | Diagnostics accuracy |
| P2-3 | `QrScannerView` camera lifecycle (requires instrumented test) | QR scanner reliability |
| P2-4 | `PocketMicEngine.Start()` rejects invalid port range | Port validation on receiver side |
| P2-5 | `ReceiverSettings` JSON roundtrip | Settings persistence |
| P2-6 | `SingleInstance` mutex fallback to Local namespace | Cross-session detection |
| P2-7 | `DefaultDeviceSwitcher.FindVirtualCaptureDevice()` with no virtual cable | Null return path |
| P2-8 | `DiagnosticsLog.ensureStarted()` idempotency | Double-start must not duplicate collectors |
| P2-9 | `ControlChannel.subnetWarning()` for same vs different subnet | Network validation |
| P2-10 | `FabCorner.fromName()` fallback for null/unknown | UI state correctness |

---

## 3. TEST ARCHITECTURE

### Android Tests

**Unit tests** (JVM, no Android framework):
- All tests under `android/app/src/test/` — already JVM-only
- Mock: No mocking needed for pure logic. For `MicStreamingService` tests, mock `AudioRecord`, `DatagramSocket`, `WifiManager`, `PowerManager`
- Fixtures: `ByteArray` helpers for building test packets (see existing `statsPayload()` helper)

**Instrumented tests** (Android device/emulator):
- Compose UI: `MainActivity`, `DiagnosticsScreen`, `QrScannerView`
- Service lifecycle: `MicStreamingService` start/stop/reconnect
- Permissions: `RECORD_AUDIO` grant/deny paths
- `AppPrefs` with `EncryptedSharedPreferences` (requires `Context`)

**Suggested directory structure:**
```
android/app/src/test/java/com/ryanspice/pocketmic/
├── PacketCryptoTest.kt          (exists)
├── ControlProtocolTest.kt       (exists)
├── ControlChannelTest.kt        (exists)
├── ReconnectPolicyTest.kt       (exists)
├── MicConfigTest.kt             (exists)
├── SendQueueTest.kt             (exists)
├── QrParserTest.kt              ← NEW
├── StreamingStateTest.kt        ← NEW
├── CaptureModeTest.kt           ← NEW
├── AnnounceProtocolTest.kt      ← NEW
└── CrossPlatformInteropTest.kt  ← NEW (tests Android→C# wire compat)

android/app/src/androidTest/java/com/ryanspice/pocketmic/
├── MicStreamingServiceTest.kt   ← NEW (instrumented)
├── DiagnosticsLogTest.kt        ← NEW (instrumented)
└── AppPrefsTest.kt              ← NEW (instrumented)
```

### Windows Tests

**Unit tests** (xUnit, no UI):
- All tests under `windows-receiver-tests/`
- Mock: None needed for pure DSP/math functions. For `PocketMicEngine`, mock `UdpClient` and `WaveOutEvent`
- Fixtures: `SamplePcm()`, `Datagram()`, `Key()` helpers already exist

**Suggested directory structure:**
```
windows-receiver-tests/
├── AudioPipelineTests.cs        (exists)
├── ControlProtocolTests.cs      (exists)
├── AnalyticsTests.cs            (exists)
├── VoiceProcessorTests.cs       ← NEW
├── ReceiverSettingsTests.cs     ← NEW
├── SingleInstanceTests.cs       ← NEW (if not platform-locked)
├── PocketMicEngineTests.cs      ← NEW (integration)
└── CrossPlatformInteropTest.cs  ← NEW (tests C#→Android wire compat)
```

### Mocks & Fixtures

| Component | Mock Needed | Fixture Strategy |
|---|---|---|
| `AudioRecord` | Interface wrapper or fake | Pre-recorded PCM byte arrays |
| `DatagramSocket` | In-memory channel | Queue of `DatagramPacket` |
| `WifiManager.WifiInfo` | Data class stub | Fake frequency/RSSI values |
| `VoiceProcessor` | None (pure computation) | Known PCM input → assert output samples |
| `PocketMicEngine` | `UdpClient` wrapper | Pre-built encrypted datagrams |
| `WaveOutEvent` | NAudio interface | Silent output buffer |
| Cross-platform | None | Android builds packet → C# decodes, and vice versa |

---

## 4. SPECIFIC TEST CASES

### QrParserTest.kt (P0-1)
```kotlin
@Test
fun parseValidPmicUriWithHostPortAndKey() {
    // pmic://192.168.1.25:49500/POCKETMIC-TEST
    // Should return QrPairingData("192.168.1.25", 49_500, "POCKETMIC-TEST")
}

@Test
fun parseWithoutSchemePrefixAcceptsPlainHostPortKey() {
    // "192.168.1.25:49500/POCKETMIC-TEST" should parse correctly
}

@Test
fun parseRejectsKeyShorterThanEightCharacters() {
    // "192.168.1.25:49500/SHORT" should return null
}
```

### StreamingStateTest.kt (P1-8)
```kotlin
@Test
fun updateTransformsSnapshotUsingLatestState() {
    // Calling update { it.copy(status = STREAMING) } then update { it.copy(level = 0.5f) }
    // should produce status=STREAMING AND level=0.5f
}

@Test
fun resetReturnsToIdleDefaults() {
    // After setting various fields, reset() should return a StreamingSnapshot with all defaults
}

@Test
fun concurrentUpdatesDoNotLoseIntermediateValues() {
    // Launch two coroutines calling update simultaneously; both transforms should apply
}
```

### CaptureModeTest.kt (P1-9)
```kotlin
@Test
fun fromWireValueReturnsCorrectModeForEachValue() {
    // "clean" → CLEAN, "voice" → VOICE, "custom" → CUSTOM
}

@Test
fun fromWireValueFallsBackToCleanForUnknown() {
    // "unknown_mode" → CLEAN, null → CLEAN
}
```

### AnnounceProtocolTest.kt (P0-3, P0-4)
```kotlin
@Test
fun audioProtocolMatchesTrueWhenVersionsAreEqual() {
    // announce with audioProtocolVersion == PacketCrypto.VERSION → audioProtocolMatches == true
}

@Test
fun audioProtocolMatchesFalseWhenVersionsDiffer() {
    // announce with audioProtocolVersion = 99 → audioProtocolMatches == false
}

@Test
fun announceWithEmptyHostNameStillDecodes() {
    // announcePayload(nonce, 49500, "") should decode with hostName = ""
}
```

### VoiceProcessorTests.cs (P1-1)
```csharp
[Fact]
public void ProcessPassesThroughSilentInputUnchanged()
{
    // All-zero PCM input → output should remain all-zero (DC blocker, gate, limiter are no-ops on silence)
}

[Fact]
public void HighPassFilterAttenuatesLowFrequenciesBelowCutoff()
{
    // Generate 10Hz sine wave in PCM → after Process(), amplitude should be significantly reduced
}

[Fact]
public void LimiterClipsSignalAtCeiling()
{
    // Input with samples above 0.891 * 32768 → output samples clamped to ceiling
}
```

### ReceiverSettingsTests.cs (P2-5)
```csharp
[Fact]
public void SaveAndLoadRoundTripsAllSettings()
{
    // Set non-default values, Save(), Load(), assert all fields match
}

[Fact]
public void LoadReturnsDefaultsForMissingFile()
{
    // Delete settings file → Load() returns new ReceiverSettings() with defaults
}

[Fact]
public void LoadReturnsDefaultsForCorruptFile()
{
    // Write garbage to settings path → Load() returns defaults, no exception
}
```

### CrossPlatformInteropTest.cs (P0-2)
```kotlin
// Android side — Kotlin
@Test
fun androidControlFrameDecodesOnWindowsLayout() {
    // Build a probe frame with ControlProtocol.build(), verify the byte layout matches
    // what ControlProtocol.TryParse expects on C# side (magic, version, type, length)
}

@Test
fun androidAnnouncePayloadDecodesOnWindowsLayout() {
    // Build announce payload, verify nonce+port+name+trailer byte offsets match C# reader
}
```

---

## 5. QUICK WINS

These tests can be written today with **zero new dependencies** — they only test pure functions already exposed as public or `internal`:

| Test File | Method | What It Proves | LOC |
|---|---|---|---|
| `QrParserTest.kt` | `parseValidPmicUri` | QR onboarding works | ~10 |
| `QrParserTest.kt` | `parseWithoutScheme` | Convenience format works | ~10 |
| `QrParserTest.kt` | `parseRejectsShortKey` | Security boundary | ~8 |
| `QrParserTest.kt` | `parseDefaultPortWhenOmitted` | Port fallback | ~8 |
| `QrParserTest.kt` | `parseRejectsPortOutOfRange` | Validation | ~8 |
| `CaptureModeTest.kt` | `fromWireValueAllModes` | Enum mapping | ~10 |
| `CaptureModeTest.kt` | `fromWireValueUnknownFallback` | Safe fallback | ~5 |
| `StreamingStateTest.kt` | `updateComposesTransforms` | State correctness | ~10 |
| `StreamingStateTest.kt` | `resetReturnsDefaults` | Reset correctness | ~8 |
| `AnnounceProtocolTest.kt` | `audioProtocolMatchesWhenEqual` | Protocol negotiation | ~10 |
| `AnnounceProtocolTest.kt` | `audioProtocolMismatchWhenDifferent` | Protocol negotiation | ~10 |
| `AnnounceProtocolTest.kt` | `announceWithEmptyName` | Edge case | ~8 |
| `AnnounceProtocolTest.kt` | `announceWithEmptyBuildVersion` | Edge case | ~8 |
| `PacketCryptoTest.kt` | `deriveKeyDeterministic` | Key derivation | ~5 |
| `PacketCryptoTest.kt` | `deriveKeyDifferentInputDifferentKey` | Key isolation | ~5 |
| `PacketCryptoTest.kt` | `encryptorRejectsZeroPcmBytes` | Input validation | ~10 |
| `MicConfigTest.kt` | `captureModeFromWireValueAllModes` | Enum mapping | ~10 |
| `MicConfigTest.kt` | `dspSettingsDefaults` | Default values | ~8 |
| `MicConfigTest.kt` | `fabCornerFromNameFallback` | Safe fallback | ~8 |
| `ControlChannelTest.kt` | `nonceRingCapacityOne` | Degenerate ring | ~10 |
| `ControlChannelTest.kt` | `nonceRingLargeCapacity` | Ring behavior | ~10 |
| `VoiceProcessorTests.cs` | `ProcessSilentInputPassesThrough` | No-op on silence | ~10 |
| `VoiceProcessorTests.cs` | `LimiterClipsAtCeiling` | Limiter correctness | ~10 |
| `VoiceProcessorTests.cs` | `ApplyRedesignsOnlyOnChange` | Performance | ~10 |
| `ReceiverSettingsTests.cs` | `LoadReturnsDefaultsForMissingFile` | Error recovery | ~8 |
| `ReceiverSettingsTests.cs` | `DefaultValuesAreCorrect` | Contract | ~10 |

**Total: ~26 quick-win tests, approximately 220 lines of code.**

---

## 6. PRIORITY RECOMMENDATION

### Phase 1 (This week) — Quick wins + P0 security gaps
- All 26 quick-win tests from §5
- `QrParserTest.kt` (5 tests) — highest risk onboarding path
- `AnnounceProtocolTest.kt` (4 tests) — protocol correctness
- `VoiceProcessorTests.cs` (3 tests) — DSP correctness
- Cross-platform interop test (1 test) — wire format contract

### Phase 2 (Next sprint) — P1 audio pipeline
- `VoiceProcessorTests.cs` full suite (~15 tests)
- `StreamingStateTest.kt` state management
- `CaptureModeTest.kt` enum mapping
- `MicStreamingService` unit tests with mocked dependencies
- Sequence rollover test

### Phase 3 (Following) — P2 integration & UI
- `ReceiverSettingsTests.cs` JSON persistence
- `SingleInstanceTests.cs` mutex behavior
- `PocketMicEngineTests.cs` integration with mocked sockets
- Instrumented tests for Compose UI and service lifecycle

---

## 7. SUMMARY

**Current coverage:**
- **Android**: 6 test files covering PacketCrypto, ControlProtocol, ControlChannel (ProbeNonceRing only), ReconnectPolicy, MicConfig, SendQueue — **11 source files untested**
- **Windows**: 3 test files covering AudioPipeline, ControlProtocol, Analytics — **5 source files untested** (VoiceProcessor, PocketMicEngine, ReceiverSettings, SingleInstance, DefaultDeviceSwitcher)

**Highest-risk untested areas:**
1. `parseQrPairingData()` — the entire pairing flow
2. `VoiceProcessor` DSP chain — audio quality
3. `PocketMicEngine` receive loop — end-to-end audio path
4. Cross-platform wire format compatibility
5. `MicStreamingService` audio source fallback, Wi-Fi lock policy, reconnect redirect
