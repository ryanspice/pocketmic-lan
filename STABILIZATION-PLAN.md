# PocketMic LAN v0.1.5 — Stabilization Plan

> Created 2026-09-11 from Astra 6 Pro review findings.
> This supersedes MASTER-PLAN.md phases 6-8 with evidence-gated stages.
> **This is a validation-only pass. Do not add features, change ciphers, or publish.**

---

## Guiding Principle

"Implemented" ≠ "Merged" ≠ "Builds" ≠ "Validated" ≠ "Release-ready"

Each stage requires **evidence** (command output, checksums, traces) before proceeding.

---

## Stage 6A — Build Baseline

**Exit criterion**: Fresh checkout produces installable Android APK and Windows package that exercises native encode/decode without undocumented dependencies.

### Tasks

| # | Task | Evidence required | Status |
|---|------|-------------------|--------|
| 6A.1 | Pin libopus version and source | Version number, download URL, SHA-256 of source tarball | ⬜ |
| 6A.2 | Record toolchain versions | NDK version, CMake version, MSVC/MinGW version | ⬜ |
| 6A.3 | Script dependency acquisition | `scripts/setup-libopus.ps1` or equivalent, tested on clean machine | ⬜ |
| 6A.4 | Verify `opus.dll` filename | P/Invoke resolves correctly; smoke test loads the DLL | ⬜ |
| 6A.5 | Android: build with NDK | `./gradlew assembleDebug` succeeds, APK installs | ⬜ |
| 6A.6 | Android: verify native library | `adb shell run-as com.ryanspice.pocketmic ls lib/arm64-v8a/` shows `libopus.so` | ⬜ |
| 6A.7 | Windows: build full receiver | `dotnet publish` produces self-contained ZIP with `opus.dll` | ⬜ |
| 6A.8 | Windows: native smoke test | Fresh-process: load DLL, create decoder, decode one frame, destroy | ⬜ |
| 6A.9 | Ship upstream licenses | Include Opus BSD license in both artifacts | ⬜ |
| 6A.10 | Record all evidence | Commands, commits, checksums, environment in a `BUILD-EVIDENCE.md` | ⬜ |

### Libopus version decision

Use **libopus 1.5.2** (not 1.6.1) for this release because:
- The research synthesis was written against 1.5.x API docs
- The CMakeLists.txt and JNI wrapper were designed for 1.5.x
- Upgrading to 1.6.1 is a separate change that needs its own validation
- Record this rationale in BUILD-EVIDENCE.md

### Supported ABIs (Android)

```
arm64-v8a    (primary target)
armeabi-v7a  (compatibility)
x86_64       (emulator)
```

### Windows architecture

```
win-x64      (primary target)
```

---

## Stage 6B — Protocol & Codec Correctness

**Exit criterion**: Cross-language packet fixtures pass; session lifecycle is safe; decode ordering is correct.

### 6B.1 — Cross-language test vectors

Create `tests/fixtures/` with fixed synthetic packets:

| Vector | Description | Contains |
|--------|-------------|----------|
| `v1-pcm.dat` | Standard v1 PCM16 packet | key, nonce, header/AAD, plaintext (960B), ciphertext, tag |
| `v2-opus.dat` | Standard v2 Opus packet | key, nonce, header/AAD, opus payload (~60B), ciphertext, tag |
| `v2-pcm-fallback.dat` | v2 with PCM codec flag | Tests PCM-over-v2 path |

For each vector, record:
- Key (32 bytes, hex)
- Salt (4 bytes, hex)
- Session ID (8 bytes, hex)
- Sequence (4 bytes, hex)
- Header bytes (24 or 28 bytes, hex)
- AAD bytes (hex)
- Plaintext (hex)
- Ciphertext + tag (hex)

**Test in both Kotlin and C#**: encrypt in Kotlin → decrypt in C# and vice versa.

### 6B.2 — Mutated packet tests

For each vector, create mutations and verify rejection:

| Mutation | Expected behavior |
|----------|-------------------|
| Flip 1 bit in header | Reject (AAD mismatch) |
| Flip 1 bit in ciphertext | Reject (GCM tag mismatch) |
| Flip 1 bit in tag | Reject (GCM tag mismatch) |
| Truncate payload | Reject (length mismatch) |
| Append extra bytes | Reject (length mismatch) |
| Wrong key | Reject |
| Wrong sequence (replay) | Reject |
| Previous session traffic | Reject (different session ID) |
| Version 0 or 3+ | Reject (unsupported version) |
| v2 with payloadLength > datagram | Reject (bounds check) |

### 6B.3 — Nonce lifecycle verification

| Scenario | Expected behavior |
|----------|-------------------|
| Normal sequence (0, 1, 2, ...) | Accept |
| Replayed sequence | Reject |
| Skipped sequence (0, 5) | Accept (gap is allowed, not a replay) |
| Counter rollover (0xFFFFFFFF → 0) | Document behavior; either accept with new session or reject |
| Reconnect (new session ID) | New nonce space; old traffic rejected |
| Service restart (same key, new session) | New nonce space |
| Codec change mid-session | New session required? Or negotiated transition? |

### 6B.4 — Decode ordering verification

**Critical**: Opus is stateful and requires serial, in-order decoding.

Verify this sequence in the receiver:
```
1. Receive packet seq=100 → decode → PCM output
2. Receive packet seq=102 → queue (not yet decoded)
3. Deadline for seq=101 → PLC generates concealment
4. Receive packet seq=101 (late) → DISCARD (do not decode)
5. Deadline for seq=102 → decode queued packet → PCM output
```

**Test**: send packets 1, 2, 4, 5 (skip 3), then deliver 3 late.
Expected: PLC for 3, decode 4 and 5 in order, discard late 3.

### 6B.5 — Fallback transition contract

Define and test these scenarios:

| Scenario | Expected behavior |
|----------|-------------------|
| Android v2 + Windows v1 (old receiver) | Receiver rejects v2 packets; Android falls back to v1? Or session fails? |
| Android v1 + Windows v2 (new receiver) | Receiver accepts v1 packets normally |
| Both v2, `opus.dll` missing on Windows | Receiver reports "codec unavailable"; session fails or falls back? |
| Both v2, `libopus.so` missing on Android | Android falls back to v1 PCM before sending |
| Both v2, decoder creation fails mid-session | Session stops or falls back? Document the contract. |
| Both v2, encoder creation fails at startup | Android falls back to v1 PCM |

**Preferred for v0.1.5**: controlled session restart into PCM, not seamless mid-stream switching.

---

## Stage 6C — Runtime Behavior

**Exit criterion**: Deterministic impairment scenarios produce expected behavior; screen-off operation is validated.

### 6C.1 — Adaptive jitter buffer acceptance criteria

**Define the quantity being estimated**: P95 of what?

Current implementation: P95 of interarrival time deltas (time between consecutive received packets).

Test scenarios:

| Scenario | Input | Expected behavior |
|----------|-------|-------------------|
| Steady state (no loss, no jitter) | 10ms interarrival, 0% loss | Target converges to ~12ms (P95 × 1.2) |
| Moderate jitter | 10ms ± 5ms, 0% loss | Target converges to ~18ms |
| Burst loss (50 packets) | 0% → 50 packets lost → 0% | Target increases; PLC fills gaps; recovery within 5s |
| Rapid disruption | Sudden 200ms jitter spike | Target increases (rate-limited to +5ms/100ms) |
| Recovery | Jitter spike resolves | Target decreases (rate-limited to -5ms/100ms) |
| Clock drift (sender 1% fast) | 10.1ms interarrival | Drift compensation adjusts target |

**Rate limiter math (verify against implementation)**:
- Window: 200 samples = 2s at 100 pkt/s
- Growth: +5ms per 100ms = +50ms/s
- 20ms → 200ms = 3.6s (before estimator response)
- **Question**: is this fast enough for Wi-Fi AP handoff (~200ms disruption)?

### 6C.2 — Drift compensation trace

Record and verify:
- Estimated drift (ms/s)
- Applied correction (ms)
- Queue occupancy (packets) over time
- Scenarios: synthetic clock mismatch, burst loss, reconnect, timestamp discontinuity

### 6C.3 — Packet pacing trace

Record PCAP of a 10-second stream:
- Measure actual inter-packet intervals
- Verify mean ≈ 10ms, standard deviation < 2ms
- Verify no burst patterns (catch-up resets work correctly)

### 6C.4 — Screen-off soak test

| Scenario | Duration | Measure |
|----------|----------|---------|
| Phone locked, stream active | 10 min | Dropouts, queue growth, recovery |
| Activity backgrounded | 5 min | Capture reliability |
| Repeated start/stop with screen off | 10 cycles | Lock acquisition/release |
| Battery exemption declined | Start stream | Stream works, user warned |
| Network interruption during lock | 1 disruption | Recovery behavior |

### 6C.5 — LinkQualityPolicy + AdaptiveBuffer cooperation

Verify they don't fight:
- Policy sets tier bounds (e.g., Good: 60-120ms)
- Adaptive buffer computes target within bounds
- Policy changes tier → buffer respects new bounds
- No oscillation between tiers

---

## Stage 7 — Release Candidate

**Exit criterion**: Signed APK and complete ZIP tested as distributed; all evidence recorded.

### 7.1 — Signing

- Define signing identity (debug keystore for now, record it)
- Sign the release APK (even if not Play Store)
- Verify signed APK installs on clean device
- Document upgrade path from v0.1.4

### 7.2 — Distribution artifacts

| Artifact | Contents | Verification |
|----------|----------|--------------|
| `PocketMic-v0.1.5-debug.apk` | Debug-signed, all ABIs | Install, start, connect |
| `PocketMic-v0.1.5-release.apk` | Release-signed, all ABIs | Install, start, connect |
| `PocketMicReceiver-win-x64.zip` | Receiver + `opus.dll` + VB-CABLE instructions | Extract, run, connect |
| `SHA256SUMS.txt` | All artifacts | `sha256sum -c` passes |

### 7.3 — Compatibility matrix

| Android | Windows | Expected |
|---------|---------|----------|
| v0.1.5 (Opus) | v0.1.5 (Opus) | v2 stream, Opus decode |
| v0.1.5 (PCM) | v0.1.5 (Opus) | v1 stream, PCM decode |
| v0.1.5 (Opus) | v0.1.4 (PCM only) | v1 fallback or session failure |
| v0.1.4 (PCM only) | v0.1.5 (Opus) | v1 stream, PCM decode |

---

## Stage 8 — Publication

**Exit criterion**: Tagged commit matches verified artifacts; claims are accurate.

### Procedure (from CLAIM-CORRECTIONS.md)

1. Freeze candidate commit
2. Build artifacts from that exact commit
3. Verify artifacts (checksums, install, smoke test)
4. Tag the verified commit
5. Push tag only
6. Create GitHub release with verified artifacts

### Claims to use in release notes

**Accurate**:
- "Opus codec support (opt-in, protocol v2)"
- "Adaptive jitter buffer with percentile-based delay estimation"
- "Packet pacing for more even network utilization"
- "Battery optimization guidance for long sessions"
- "MIT license"
- "Fixed --faint color contrast in light mode"

**Do NOT claim**:
- "WCAG AA compliant" (only one color was fixed)
- "12× bandwidth reduction" (actual: ~8× on wire, needs measurement)
- "Build health improved" (CI badge was hidden, not fixed)

---

## Agent Instruction for Next Pass

> Perform a validation-only stabilization pass for PocketMic LAN v0.1.5.
> Do not add features, change the cipher, migrate to Oboe, or publish a release.
>
> First establish the exact base/head commits and reproducible native builds
> (Stage 6A). Then verify protocol/session security, cross-language packet
> fixtures, ordered Opus decode/PLC, negotiated fallback, and deterministic
> jitter/drift behavior (Stages 6B, 6C).
>
> Test the packaged Windows application and an installable Android APK,
> not only the core project.
>
> Fix confirmed defects with small, scoped commits. Record each result as
> PASS, FAIL, or BLOCKED, with the command, commit, environment, and evidence.
> Keep hardware-only checks explicitly blocked when they cannot be run;
> do not convert missing evidence into a pass.
>
> Update this plan to distinguish implementation status from validation status.
> Return remaining blockers and a release recommendation. Do not tag or
> push release refs.
