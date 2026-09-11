# PocketMic LAN v0.1.5 — Stabilization Plan (v2)

> Updated 2026-09-11 after Astra 6 Pro second review.
> Corrected acceptance criteria, removed ambiguous outcomes, tightened evidence requirements.
> **Validation-only pass. Do not add features, change ciphers, or publish.**

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
| 6A.1 | Pin libopus version and source | Version number (1.5.2), download URL, SHA-256 of source tarball | ⬜ |
| 6A.2 | Record toolchain versions | NDK version, CMake version, MSVC/MinGW version | ⬜ |
| 6A.3 | Script dependency acquisition | `scripts/setup-libopus.ps1` or equivalent, tested on clean machine | ⬜ |
| 6A.4 | Verify `opus.dll` filename | P/Invoke resolves correctly; smoke test loads the DLL | ⬜ |
| 6A.5 | Android: build with NDK | `./gradlew assembleDebug` succeeds, APK produced | ⬜ |
| 6A.6 | Android: verify native libraries in APK | `apkanalyzer files list app-debug.apk` shows `lib/arm64-v8a/libopus.so`, `lib/armeabi-v7a/libopus.so`, `lib/x86_64/libopus.so` | ⬜ |
| 6A.7 | Android: encoder smoke test | On device/emulator: create encoder, encode one 10ms frame (480 samples), destroy encoder. Record output bytes. | ⬜ |
| 6A.8 | Windows: build full receiver | `dotnet publish` produces self-contained ZIP with `opus.dll` | ⬜ |
| 6A.9 | Windows: decoder smoke test | Fresh process: load DLL, create decoder, decode one frame from test vector, destroy. Record output bytes. | ⬜ |
| 6A.10 | Ship upstream licenses | Include Opus BSD license in both distribution artifacts | ⬜ |
| 6A.11 | Record all evidence | Commands, commits, checksums, environment in `BUILD-EVIDENCE.md` | ⬜ |

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

### Signing

For v0.1.5 distributed artifacts:
- **Debug APK**: signed with debug keystore. Suitable for development testing and sideloading by testers who accept the debug signature.
- **Release APK**: signed with a stable, protected release signing key. The certificate fingerprint must be recorded. The v0.1.4 → v0.1.5 upgrade path must be tested (same signing identity required for Android to allow upgrade).
- Do NOT put private keys or passwords in the evidence document.
- The distinction between "debug-signed for testing" and "release-signed for distribution" must be explicit in the artifact table. Do not label a debug-signed APK as "release."

---

## Stage 6B — Protocol & Codec Correctness

**Exit criterion**: Cross-language packet fixtures pass; session lifecycle is safe; decode ordering is correct; fallback has one defined outcome per scenario.

### 6B.1 — Cross-language test vectors

Create `tests/fixtures/` with fixed synthetic packets.

For each vector, record ALL of:
- Key (32 bytes, hex)
- Salt (4 bytes, hex)
- Session ID (8 bytes, hex)
- Sequence (4 bytes, hex)
- **Final nonce bytes** (the actual bytes passed to AES-GCM, after construction)
- **Nonce construction rule** (how session ID, salt, sequence combine into the nonce)
- Header bytes (24 or 28 bytes, hex)
- AAD bytes (exactly what is passed as AAD)
- Plaintext (hex)
- Ciphertext + tag (hex)

| Vector | Description |
|--------|-------------|
| `v1-pcm.dat` | Standard v1 PCM16 packet |
| `v2-opus.dat` | Standard v2 Opus packet |

**Test in both Kotlin and C#**: encrypt in Kotlin → decrypt in C# and vice versa. Both must produce identical encrypted bytes from the same plaintext, not merely round-trip.

**PCM-over-v2**: do NOT create a positive fixture for this. The protocol spec defines PCM as v1 and Opus as v2. If the code supports PCM-over-v2, that is an untested protocol path that should be removed or explicitly documented. Test that v2 packets without the Opus flag are rejected.

### 6B.2 — Mutated and replayed packet tests

**Separate tampering from replay.** These are different attack vectors with different rejection mechanisms.

#### Tampering tests (mutate fields, keep auth tag)

| Mutation | Expected behavior |
|----------|-------------------|
| Flip 1 bit in header | Reject (AAD mismatch → GCM auth failure) |
| Flip 1 bit in ciphertext | Reject (GCM auth failure) |
| Flip 1 bit in tag | Reject (GCM auth failure) |
| Truncate payload | Reject (length check before decrypt, or GCM failure) |
| Append extra bytes | Reject (length check) |
| Wrong key | Reject (GCM auth failure) |
| Version 0 or 3+ | Reject (unsupported version, before decrypt) |
| v2 with payloadLength > datagram | Reject (bounds check, before decrypt) |

#### Replay tests (replay unchanged, previously accepted packets)

| Scenario | Expected behavior |
|----------|-------------------|
| Replay the exact packet that was accepted as seq=5 | Reject (sequence already seen in replay window) |
| Replay a packet from a previous session (different session ID, same key) | Reject (session ID mismatch in nonce construction) |

#### State-preservation tests

These verify that rejected packets cannot mutate accepted state:

| Scenario | Expected behavior |
|----------|-------------------|
| Send forged packet with seq=0xFFFFFFFF (very high) | Reject; replay window must NOT advance to this value |
| Send legitimate packets 1, 2, 3, then replay 2 | Reject 2; packets 1 and 3 remain accepted; decoder state unchanged |
| Send packets out of order (3, 1, 2) | All accepted (in-window reordering is not replay); decoded in order 1, 2, 3 |
| Send duplicate of packet 1 after packet 3 | Reject duplicate; decoder state unchanged |

### 6B.3 — Nonce lifecycle verification

| Scenario | Expected behavior |
|----------|-------------------|
| Normal sequence (0, 1, 2, ...) | Accept; nonces are unique per packet |
| Counter rollover (0xFFFFFFFF → 0) | **Stop or establish a demonstrably fresh key/nonce space.** Do not accept packet 0 after 0xFFFFFFFF under the same key. |
| Reconnect (new session ID) | New nonce space, verified by nonce construction rule. Old traffic rejected because nonce includes new session ID. |
| Service restart (same key, new session) | Same as reconnect. |
| Codec change mid-session | **New session required.** Do not attempt seamless codec switching within a session. |

### 6B.4 — Decode ordering verification

**Critical**: Opus is stateful and requires serial, in-order decoding.

Verify this exact sequence in the receiver:
```
1. Receive packet seq=100 → authenticate → queue
2. Playback deadline for seq=100 → decode seq=100 → PCM output
3. Receive packet seq=102 → authenticate → queue
4. Playback deadline for seq=101 → seq=101 not in queue → PLC generates concealment
5. Receive packet seq=101 (late) → authenticate succeeds → but deadline passed → DISCARD (do not decode)
6. Playback deadline for seq=102 → decode seq=102 → PCM output
```

**Test**: send packets 1, 2, 4, 5 (skip 3), then deliver 3 late.
Expected: PLC for 3, decode 4 and 5 in order, discard late 3.

**Also verify**: PLC requests the correct duration (480 samples = 10ms at 48kHz), not an arbitrary buffer size.

### 6B.5 — Fallback transition contract

**One expected outcome per scenario. No alternatives. No question marks.**

| Scenario | Expected outcome |
|----------|-----------------|
| Android v2 + Windows v1 (old receiver) | **Session fails.** Old receiver does not understand v2. Android must detect the failure (no ACK, or control-channel negotiation) and report incompatibility. Does NOT silently fall back. |
| Android v1 + Windows v2 (new receiver) | **Session works.** Receiver accepts v1 packets normally. |
| Both v2, `opus.dll` missing on Windows | **Session fails at startup.** Receiver reports "Opus codec unavailable: opus.dll not found." Does NOT silently fall back. |
| Both v2, `libopus.so` missing on Android | **Android falls back to v1 PCM before sending first packet.** OpusEncoder.create() returns null; service uses PCM path. |
| Both v2, encoder creation fails at startup | **Android falls back to v1 PCM before sending first packet.** Same as above. |
| Both v2, encoder fails mid-session | **Session stops.** Report error to user. Do NOT attempt seamless codec switch. |
| Both v2, decoder fails mid-session | **Session stops.** Report error to user. |

**Rationale for "session fails" over "silent fallback"**: silent fallback masks incompatibility and creates unpredictable behavior. A clear failure with a descriptive message is better than a degraded session the user doesn't understand.

---

## Stage 6C — Runtime Behavior

**Exit criterion**: Deterministic impairment scenarios produce expected behavior; sustained screen-off operation is validated.

### 6C.1 — Adaptive jitter buffer acceptance criteria

**Define the quantity being estimated**: P95 of interarrival time deltas (time between consecutive received packets). This is the raw estimator output, not the playback target.

**Distinguish**:
- **Raw estimator output**: P95 × 1.2 of the interarrival sample window
- **Bounded playback target**: raw output clamped to [tier_min, tier_max] from LinkQualityPolicy
- **Actual queue occupancy**: number of packets buffered
- **Applied playback correction**: drift compensation adjustment

The bounded target is what actually controls playback. The raw estimator can output12ms, but if the policy tier is Good (60-120ms), the bounded target is60ms.

#### Test scenarios with deterministic traces

Each test uses a **fixed, deterministic arrival trace** (not a random distribution). Record the trace, raw estimate at each step, bounded target, queue occupancy, and applied correction.

| # | Scenario | Trace | Expected raw estimate | Expected bounded target |
|---|----------|-------|----------------------|------------------------|
| 1 | Steady state, no jitter | 200 packets at exactly 10ms intervals | P95 = 10ms, raw = 12ms | Clamped to tier min (e.g., 60ms if Good tier) |
| 2 | Moderate jitter | 200 packets: 150 at 10ms, 50 at 15ms (deterministic interleaving) | P95 ≈ 15ms, raw ≈ 18ms | Clamped to tier range |
| 3 | Single 500ms gap | 199 packets at 10ms, 1 gap of 510ms | P95 = 10ms (the gap is the 100th percentile, not 95th) | Raw remains 12ms; the gap triggers PLC, not a target change |
| 4 | 5% uniform loss | 200 packets at 10ms, every 20th dropped | P95 = 10ms (loss doesn't change interarrival of received packets) | Raw remains 12ms; loss triggers PLC |
| 5 | Sudden 200ms jitter | 100 packets at 10ms, then 50 packets at 210ms intervals | P95 climbs; rate-limited to +5ms/100ms | Bounded target increases within tier |
| 6 | Recovery after spike | Scenario 5 continues with 100 packets at 10ms | P95 decreases; rate-limited to -5ms/100ms | Bounded target decreases |
| 7 | Clock drift (sender 1% fast) | 200 packets at 9.901ms intervals (10/1.01) | P95 ≈ 9.9ms, raw ≈ 11.9ms | Drift compensation detects persistent negative trend |

**Note on scenario3**: a single outage does NOT raise P95 in a200-sample window. The95th percentile of199 values of10ms and1 value of510ms is10ms. Loss triggers PLC for the missing packet; it does not change the interarrival estimate of received packets. This is correct behavior — the estimator should be robust to individual losses.

**Note on scenario7**: sender1% fast means packets arrive at10/1.01 ≈ 9.901ms, not10.1ms. A faster sender produces shorter intervals.

### 6C.2 — Drift compensation trace

Record over a60-second test with1% clock mismatch:
- **Estimated drift** (ms/s): the linear regression slope
- **Applied correction** (ms): how the target is adjusted
- **Queue occupancy** (packets): over time
- **Playback rate adjustment**: if any sample-rate correction is applied

Show that the correction prevents unbounded queue growth or depletion.

### 6C.3 — Packet pacing trace

Record PCAP of a10-second stream:
- Measure actual inter-packet intervals
- Verify mean ≈ 10ms
- Verify no burst patterns (catch-up resets work correctly)
- Record standard deviation

### 6C.4 — Screen-off soak test (minimum30 minutes)

| Scenario | Duration | Measure |
|----------|----------|---------|
| Phone locked, stream active | **30 min minimum** | Dropouts, queue growth, recovery, memory |
| Activity backgrounded | 5 min | Capture reliability |
| Repeated start/stop with screen off | 10 cycles | Lock acquisition/release |
| Battery exemption declined | Start stream | Stream works, user warned |
| Network interruption during lock | 1 disruption | Recovery behavior |
| After30 min, unlock and verify UI | — | UI responsive, stats accurate |

### 6C.5 — LinkQualityPolicy + AdaptiveBuffer cooperation

Verify they don't fight:
- Policy sets tier bounds (e.g., Good: 60-120ms)
- Adaptive buffer computes target within bounds
- Policy changes tier → buffer respects new bounds
- No oscillation between tiers
- One owner for playback-target changes (adaptive buffer, clamped by policy)

---

## Stage 7 — Release Candidate

**Exit criterion**: Signed APK and complete ZIP tested as distributed; all evidence recorded.

### 7.1 — Signing (explicit)

| Artifact | Signing | Purpose |
|----------|---------|---------|
| `PocketMic-v0.1.5-debug.apk` | Debug keystore | Development testing, sideloading by testers |
| `PocketMic-v0.1.5-release.apk` | **Stable release key** (protected, fingerprint recorded) | Distribution |

- Test v0.1.4 → v0.1.5 upgrade (requires same signing identity)
- Do NOT put private keys or passwords in evidence documents

### 7.2 — Distribution artifacts

| Artifact | Contents | Verification |
|----------|----------|--------------|
| `PocketMic-v0.1.5-debug.apk` | Debug-signed, all ABIs | Install, encoder smoke test, connect |
| `PocketMic-v0.1.5-release.apk` | Release-signed, all ABIs | Install, encoder smoke test, connect |
| `PocketMicReceiver-win-x64.zip` | Receiver + `opus.dll` + VB-CABLE instructions | Extract, decoder smoke test, connect |
| `SHA256SUMS.txt` | All artifacts | `sha256sum -c` passes |

### 7.3 — Compatibility matrix

| Android | Windows | Expected outcome |
|---------|---------|-----------------|
| v0.1.5 (Opus) | v0.1.5 (Opus) | v2 stream, Opus decode, works |
| v0.1.5 (PCM fallback) | v0.1.5 (Opus) | v1 stream, PCM decode, works |
| v0.1.5 (Opus) | v0.1.4 (PCM only) | **Session fails.** v0.1.4 does not understand v2. |
| v0.1.4 (PCM only) | v0.1.5 (Opus) | v1 stream, PCM decode, works |

---

## Stage 8 — Publication

**Exit criterion**: Tagged commit matches verified artifacts; claims are accurate.

### Procedure

1. Freeze candidate commit: `CANDIDATE=$(git rev-parse HEAD)`
2. Build artifacts from that exact commit
3. Verify artifacts (checksums, install, smoke tests)
4. Record `BUILD-EVIDENCE.md` with full base/head SHAs, diff command, dirty-worktree status
5. Tag the verified commit: `git tag -a v0.1.5 -m "..." $CANDIDATE`
6. Push tag only: `git push origin v0.1.5`
7. Create GitHub release with verified artifacts

### Scope reporting

Record the exact comparison:
```bash
git log --oneline $BASE_SHA..$HEAD_SHA
git diff --stat $BASE_SHA $HEAD_SHA
git status --short  # dirty-worktree check
```

Do not use relative references like `HEAD~6`. Record absolute SHAs.

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
- "12× bandwidth reduction" (modeled ~7.8× on wire; needs PCAP measurement)
- "Build health improved" (CI badge was hidden, not fixed)

### Bandwidth claim (corrected)

> At 100 packets per second, the modeled IPv4/UDP traffic is approximately 822.4 kbit/s for v1 PCM and 105.6 kbit/s for v2 Opus at a 48 kbit/s average payload rate — about 7.8× lower. This excludes link-layer overhead; real-stream measurement is pending.

---

## Evidence Requirements Summary

| Evidence type | Format | Stage |
|---------------|--------|-------|
| Build logs | Terminal output, exit codes | 6A |
| Checksums | SHA-256 hex | 6A, 7 |
| APK contents | `apkanalyzer files list` output | 6A |
| Smoke test output | Encoder/decoder output bytes | 6A |
| Test vectors | Hex dumps in `tests/fixtures/` | 6B |
| Test pass/fail | xUnit output with counts | 6B, 6C |
| PCAP traces | Packet captures with analysis | 6C |
| Jitter traces | CSV: timestamp, raw_estimate, bounded_target, queue_occupancy | 6C |
| Drift traces | CSV: timestamp, estimated_drift, applied_correction, queue_depth | 6C |
| Screen-off soak | Timer, memory, dropout count | 6C |
| Signing | Certificate fingerprint, NOT private keys | 7 |

### Hardware-blocked vs agent-runnable

An agent without a phone **can still complete**:
- All Stage 6A tasks except 6A.6, 6A.7 (Android device/emulator required)
- All Stage 6B tasks (protocol fixtures, unit tests — no hardware needed)
- All Stage 6C deterministic tests (simulated traces, not real network)
- Stage 6C.1-6C.3, 6C.5 (code-level verification)

An agent **cannot complete** without hardware:
- 6A.6, 6A.7 (Android APK install + encoder smoke test)
- 6C.4 (screen-off soak)
- 7.2 (install + connect test)

**Mark hardware-blocked tasks as BLOCKED, not FAILED.** The agent should complete everything it can and report remaining BLOCKED items.

---

## Agent Instruction

> Perform a validation-only stabilization pass for PocketMic LAN v0.1.5.
> Do not add features, change the cipher, migrate to Oboe, or publish a release.
>
> Before writing acceptance tests, correct the remaining ambiguities in
> STABILIZATION-PLAN.md and CLAIM-CORRECTIONS.md:
> - Separate raw jitter estimates from bounded playback targets
> - Specify deterministic arrival traces
> - Correct the clock-drift direction (1% fast = 9.901ms intervals)
> - Do not assume a single outage raises P95
> - Select one expected outcome for every fallback scenario
> - Confirm PCM-over-v2 is not a supported wire format
>
> Then execute Stage 6A and continue through independently runnable validation.
> Fix confirmed defects with small, scoped commits. Record each result as
> PASS, FAIL, or BLOCKED, with the command, commit, environment, and evidence.
> Keep hardware-only checks explicitly BLOCKED; do not convert missing
> evidence into a pass. An agent without hardware should still complete
> protocol fixtures and deterministic tests.
>
> Add exact nonce construction and fixed-byte fixture assertions. Separate
> replay from tamper tests. Verify that rejected packets cannot mutate
> accepted session, replay, codec, or playback state.
>
> Replace Android filesystem-based library verification with APK-content
> inspection plus an actual encoder smoke test. Clarify debug versus
> release signing. Correct the bandwidth summary and verify file counts
> using explicit base/head SHAs.
>
> Update the plan to distinguish implementation status from validation status.
> Return remaining blockers and a release recommendation. Do not tag or
> push release refs.
