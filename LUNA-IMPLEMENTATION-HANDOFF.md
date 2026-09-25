# PocketMic correctness and desktop controls — Luna implementation handoff

Version: 1.1.0. Prepared 2026-09-24 from commit `ddc8f38`; includes user-requested fresh discovery and pre-Start handshake behavior.

## Copy-ready task prompt

> Implement this handoff sequentially in the current PocketMic repository. First read applicable AGENTS instructions, this file, `DESKTOP-CONTROL-REVIEW.md`, and the named current source. Inspect git status and preserve unrelated work. Treat examples below as implementation guidance, not code to execute blindly. Work in order A, B, C, D, E, F, G, I, H, running focused tests after each packet. Keep production edits targeted; do not rewrite the UI or change frameworks. Do not commit, push, publish, or install drivers. Keep hardware acceptance explicitly separate from source/unit checks. Report exact changed files, commands/results, and remaining acceptance rows. If a suggested API name differs from current source, adapt the smallest relevant caller set and explain it. Do not mark the work complete merely because the existing 146 tests pass.

The task is appropriate for sequential Luna implementation because the behavior, ownership, order, and test cases are specified here. This document does not dispatch an agent or change this task's model. A separate reviewer should assess the finished diff; a producing model's self-review is not independent acceptance.

## Desired product behavior (proposed decisions)

These are explicit proposed decisions for implementation, not claims about current behavior:

1. Preserve automatic buffering by default. Add an **Automatic buffer** checkbox; when unchecked, the existing slider is an exact manual target. Show actual effective buffer separately from link-policy recommendations. Do not silently reinterpret the slider as an undocumented minimum.
2. Monitor enable and monitor device changes apply immediately. A failed device open must not leave a false selected/active label or persist a device that never opened. Playback output remains locked during a run.
3. Phone Custom DSP settings remain Custom until the user deliberately moves desktop Strength. That action restores preset DSP and applies the displayed strength. Reflecting a phone update in the UI must not count as a desktop override.
4. The Windows microphone action targets only the recording endpoint corresponding to the selected playback cable. Ambiguity disables the convenience action with actionable text. It must never pick the first unrelated virtual microphone.
5. Use/Restore snapshots all three Windows capture roles and rolls back partial failures. Explicit Restore restores the snapshot per role. On application exit, restore only role assignments still owned by this run; preserve later external changes. Hiding to tray is not exit.
6. Android keeps PCM as its compatible default but exposes PCM/Opus selection and carries it through preferences, UI, start intent, and service. Changing codec requires a new stream session. Midstream encode failure stops with an error; no same-session codec swap.
7. User-requested connection behavior: changing Auto-connect clears stale discovered target/status and creates a fresh discovery attempt when enabled. Discovery and preliminary handshake run before microphone Start. Interpret clearing as clearing derived IP/port/peer/session evidence, not silently erasing the saved pairing credential. An explicit Forget pairing action is separate. Discover an unpaired receiver as a candidate and show Pairing required/mismatch; never distribute the pairing key in a LAN broadcast.

If the user changes these decisions, revise the plan and acceptance tests before implementing the affected packet. Do not invent additional modes, DSP features, driver installers, or a new protocol version.

## Baseline and commands

The first `dotnet` on PATH is an x86 runtime without an SDK. Use the explicit x64 SDK:

```powershell
$dotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
& $dotnetExe build windows-receiver/PocketMicReceiver.csproj --no-restore --verbosity minimal
& $dotnetExe test windows-receiver-tests/PocketMicReceiver.Tests.csproj --no-restore --verbosity minimal

$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:Path = ($env:Path -replace '"', '')
Push-Location android
try { .\gradlew.bat --offline --no-daemon --console=plain testDebugUnitTest } finally { Pop-Location }
```

Do not remove `--offline` or install dependencies blindly if cache/toolchain requirements differ. Inspect the concrete error and the repo's build/setup scripts first. `scripts/build-android.ps1` also copies artifacts and can clean; do not use it as an unexplained unit-test shortcut.

Current evidence: desktop build 0 warnings/errors; Windows 146/146 pass. Prior Android run: 79 total, one failure at `StreamingStateTest.kt:39`, expected empty error string versus default null. This is a pre-existing assertion mismatch, not proof reset is broken. Prefer updating the test to the nullable contract after confirming UI consumers expect null. Do not change runtime error semantics merely to satisfy a stale assertion.

## Packet A — authentication bounds and decoder lifetime (first)

Files: `windows-receiver-core/AudioPipeline.cs`, `PocketMicEngine.cs`, `OpusDecoder.cs`; existing Windows audio/protocol tests plus focused engine lifecycle tests.

### Exact changes

1. In `TryDecryptV2`, replace the 1,024-byte allowance with the supported 512-byte protocol cap and the supplied destination capacity. Do this before slicing, decrypting, or decoding. Require the Opus flag and reject unsupported flags consistently with the documented v2 contract.
2. Preserve complete-header AAD: all 28 bytes, including payload length. Do not encrypt the header or remove payload length from authentication.
3. In `StopCoreAsync`, move decoder disposal/nulling below the awaited receive task. Cancellation alone is not synchronization with native code. Keep the decoder exclusively owned by the receive loop during a run; prevent live replacement through the public property or clearly restrict that seam to pre-start injection.
4. Handle native load failures deliberately. `DllNotFoundException`, `EntryPointNotFoundException`, and `BadImageFormatException` are not `InvalidOperationException`. Allow PCM operation without the optional decoder and report an actionable codec-unavailable error when Opus is requested. Do not blame a valid pairing key.

Example bounds guard, adapted to actual destination names:

```csharp
if (payloadLength <= 0 ||
    payloadLength > MaxOpusPayloadBytes ||
    payloadLength > opusBuffer.Length)
    return false;
if (data.Length != HeaderSizeV2 + payloadLength + TagSize)
    return false;
```

### Tests / acceptance

- Unauthenticated v2 datagrams with payload lengths -1, 0, 513, 1024 and mismatched datagram lengths return false without throwing or calling the decoder. Include 513 with a correctly sized 557-byte datagram; the old code throws before checking its tag.
- Valid encrypted 1..512-byte test payloads reach a fake decoder; do not pretend arbitrary payload bytes are valid native Opus.
- Valid packet then malformed packet then valid packet: receive loop stays alive and counts the rejection.
- A blocking fake decoder signals entry; Stop is requested; Dispose must remain uncalled until decoding is released and the loop exits. Do not exercise a real native use-after-free as a test.
- Native-missing PCM startup has a deterministic, documented outcome; actual native Opus smoke remains a separate test.

## Packet B — authenticate, order, then decode

Files: `AudioPipeline.cs`, `PocketMicEngine.cs`, `IOpusDecoder.cs`, `OpusDecoder.cs`, protocol tests and new packet-flow regression tests.

### Exact changes

1. Separate AEAD authentication/plaintext extraction from stateful Opus decoding. Add a small metadata record and an authentication method; preserve or adapt the existing public wrapper used by current tests rather than breaking every caller gratuitously.
2. Suggested boundary (names may be adapted):

```csharp
public readonly record struct AudioPacketInfo(
    ulong SessionId, uint Sequence, int SampleRate,
    AudioPipeline.Codec Codec, int PayloadLength);

// Destination is reused, large enough for PCM. The Opus protocol cap is still 512.
public static bool TryAuthenticate(
    AesGcm aes, byte[] datagram, byte[] nonce, byte[] payload,
    out AudioPacketInfo info);
```

3. The receive path must execute this order:

```text
validate structure -> authenticate into reusable payload
-> validate sample rate/codec/session policy
-> discard duplicate/late packet without touching decoder
-> reset decoder BEFORE first decode of an accepted new session
-> generate each missing Opus frame in chronological order
-> decode current Opus frame (or copy PCM)
-> process/buffer audio -> advance accepted sequence
```

4. Retain the existing policy of dropping late packets; do not add a reorder queue in this packet. Authentication failure must never mutate accepted session/sequence/decoder state. Different codec within one session is rejected; a new codec requires a new session.
5. Use Opus PLC only for an Opus stream. A decoder merely being installed does not mean a PCM stream has decoder history. PCM gaps must continue to use the existing PCM concealment path.
6. A large gap resets before decoding the surviving packet. Clear stale last-good PCM on a new session/resync. Require native decode output to match the 480-sample frame contract; do not return success with a short decoded frame and stale tail bytes.
7. Do not claim in-band FEC recovery: the current decoder passes `decode_fec=0`. Implementing an FEC playout policy is outside this packet unless separately specified.

### Exact trace expectations (fake decoder)

| Input | Expected calls |
|---|---|
| New session, seq 0 | Reset, Decode(0) |
| seq 0 repeated | No Decode, no PLC, no Reset |
| seq 2 after seq 0 | PLC(for 1), Decode(2) |
| late seq 1 after seq 2 | No decoder calls |
| Gap above concealment ceiling | Reset, Decode(current) |
| Bad tag / invalid rate | No decoder calls or accepted-state changes |
| PCM seq 0, then seq 2 | PCM concealment, zero Opus PLC calls |
| New session after prior audio | Reset before new frame; no stale previous-session concealment |

Use a small testable packet-processing seam if needed; do not write tests that only assert source strings or mock away the ordering under test. The engine must call the same seam exercised by tests.

## Packet C — Android Opus selection and signal path

Files: `android/app/src/main/java/com/ryanspice/pocketmic/{MainActivity,MicConfig,MicStreamingService,OpusEncoder,PacketCrypto}.kt`, `android/app/src/main/cpp/opus_jni.c`, corresponding JVM tests.

### Exact changes

1. Initialize codec UI state from `AppPrefs.load(...).codec`. Add a plainly labeled PCM/Opus selector disabled while the stream is active, using the existing Compose styling.
2. Carry that state into every `MicConfig` construction in `MainActivity` (manual start, discovery save, QR save). Preserve the codec when connection fields change; do not implicitly reset it to PCM.
3. Add to `startService`:

```kotlin
.putExtra(MicStreamingService.EXTRA_CODEC, config.codec.wireValue)
```

4. Native library availability must be checked inside a fallible startup boundary. The current companion `init { System.loadLibrary(...) }` can throw before `create()` returns null. Move guarded loading into the startup factory (cache the result); catch the specific linkage failure there. A nullable factory result selects PCM before creating the session encryptor. Surface the active codec/fallback honestly.
5. Apply input gain to the samples actually sent to the Opus encoder. Currently gain affects `pcmBytes` and the meter but `encode(samples, ...)` consumes the unscaled `ShortArray`. Write each clamped scaled sample back to `samples[index]` (or use a dedicated reused scaled buffer) before either encoding path. Preserve clipping bounds.
6. Before native encode, validate array length against frame length and channel count; bound output size consistently with the packet cap. Keep create/encode/destroy on one owner. Keep double release safe. No `GetPrimitiveArrayCritical` optimization is needed for this correctness work.
7. Encoding failure after a stream starts stops with an error; it must not reset sequence or switch codec under the same session.

### Acceptance

- PCM/Opus preference round trip; discovery/QR edits preserve it; start intent carries it.
- Fake encoder receives gained/clamped samples (0.5x, 1x, 3x) matching PCM semantics.
- Missing library at startup produces an explicit PCM fallback; encode failure midstream stops.
- Android unit tests and lint, native build for declared ABIs, then physical encode/decode receipt. JVM fakes do not establish JNI or audible success.

## Packet D — buffer ownership and drift (D1 plus prior drift finding)

Files: `AdaptiveJitterBuffer.cs`, `LinkQualityPolicy.cs`, `PocketMicEngine.cs`, `ReceiverSettings.cs`, desktop `Program.cs`, jitter/policy/settings tests.

### Exact changes

1. Add persisted `AutomaticBuffer` defaulting true, an engine buffering-mode property, and a desktop checkbox near the slider. Disable the manual slider in automatic mode; enable it in manual mode. Preserve its saved manual value independently from effective adaptive values.
2. Manual mode: no receive-loop or analytics callback overwrites the selected target. Automatic mode: only valid tier bounds constrain the adaptive target. Derive high water from the actual applied target in one place.
3. Every `LinkAssessment`, including insufficient-data results, carries valid current-tier bounds. Never send `(0,0)` into the engine. Validate user bounds, and preserve user settings across Start/Stop.
4. UI displays mode and effective target. Keep “recommended” explicitly separate; do not label `assessment.Prebuffer` as the applied value. Update UI at the existing throttled stats cadence.
5. Sample drift depth once per 100 ms adjustment cycle, storing timestamp plus depth in a 50-entry ring. Iterate oldest to newest. Regress depth against elapsed seconds, not array indices multiplied by an assumed rate. Use the injected/recorded time for warmup, not a second unrelated wall-clock call.
6. Suggested calculation after chronological iteration:

```csharp
double x = (sample.Tick - oldest.Tick) / (double)Stopwatch.Frequency;
// y = sample.DepthMs; OLS slope is directly milliseconds per second.
```

7. Do not call this full sample-clock correction: adjusting a target alone does not change playback sample rate. Verify whether the chosen correction actually changes buffer depth in the existing playback mechanism. If not, expose drift as diagnostics and remove the claim of active clock compensation until a bounded correction design is separately accepted. Do not casually add a resampler.

### Acceptance

- Manual 40/100/200/300 ms survives packets, all link tiers, insufficient-data windows, and stop/start.
- Automatic targets stay within valid bounds; labels match actual effective target.
- A minimum two-second span is required; a synthetic +2 ms/s trace reports approximately +2 before and after multiple ring wraps; negative and constant traces behave likewise.
- Irregular arrivals/burst loss use actual timestamps and never reverse a steadily growing trace merely because the ring wrapped.
- Saved manual value is not overwritten when automatic mode adjusts effective buffering.
- Verify startup and recovery at the 300 ms boundary against the actual `BufferedWaveProvider` capacity; avoid unreachable thresholds.

## Packet E — desktop DSP ownership (D3)

Files: `VoiceProcessor.cs`, `PocketMicEngine.cs`, `Program.cs`, `VoiceProcessorTests.cs` and focused UI/presenter tests.

1. Add a deliberate `ApplyPreset(enabled, strength)` operation that clears `UseCustom`, sets strength/enabled, and restores preset HPF/presence values (currently 85 Hz and 3.5 dB). Do not merely set `UseCustom=false` while leaving Custom filter coefficients behind.
2. Route desktop Strength user changes through this operation. Keep a simple suppression flag around programmatic control updates in `DspConfigReceived` and initial settings hydration.
3. Programmatic checkbox/slider reflection must never call back into the processor as a user preset override. Update label, checkbox, and enabled state together.
4. Prevent `ApplySettings` events from overwriting values that have not yet been loaded. Capture a settings snapshot or suppress persistence until hydration completes (especially VoiceEnhance=false with saved non-default strength).
5. Apply DSP configuration coherently at packet boundaries, or under a narrow consistent synchronization mechanism; avoid partially updated filter coefficients during processing. Keep the solution small and testable.

Acceptance: Custom -> desktop strength 90 changes effective preset gate/compression/makeup to 0.9 and restores preset filters; phone Custom update remains Custom; checkbox bypass works; reopening with enhancement off and strength 25 preserves 25; no UI feedback loop.

## Packet F — live monitor device selection (D2)

Files: `PocketMicEngine.cs`, `Program.cs`, `ReceiverSettings.cs` only if additional stable device identity is needed; focused routing/lifecycle tests.

1. Add an engine method such as `TrySetMonitorDevice(int deviceNumber, out string error)`. Update `_currentOptions` only consistently with the chosen successful state.
2. On `_monitorCombo.SelectedIndexChanged`, when not hydrating: while stopped save the selection; while running attempt to reopen the monitor on that selection.
3. On failure, report it and restore the previous selection/active monitor where possible. If restoration also fails, turn the monitor UI off with an explicit error. Main routed audio must continue.
4. Change `MonitorStarted` to report the device actually opened (or use a returned result), rather than reading the current UI combo as proof of which endpoint opened.
5. Toggling off/on uses the last successfully selected device. Reject or explicitly describe selecting the same physical endpoint as the main output, including Windows-default aliases.

Acceptance: device A -> B while streaming uses B; off/on still uses B; failed B open does not persist B; stopped selection is preserved for next start; main playback is unaffected; same-device behavior is explicit. Test via an injectable output factory plus a live device check, not just UI labels.

## Packet G — matching cable and reversible Windows defaults (D4)

Files: `DefaultDeviceSwitcher.cs`, desktop `Program.cs`, focused new routing/default-device policy tests.

1. Subscribe to playback selection changes while stopped; recompute routing text and button eligibility. `DescribeRouting` must clear stale eligibility for speakers. Do not enable the action merely because any virtual capture device exists.
2. Change the resolver to take the selected render endpoint. Prefer stable endpoint identity/device-container information. If retaining legacy WaveOut names, make supported pair mappings explicit and reject ambiguous/truncated-name matches. Never use the global first-match fallback.
3. Resolve VB-CABLE input -> its output and each supported VoiceMeeter bus -> its corresponding recording bus. Keep unfamiliar/ambiguous devices selectable for playback but disable automatic default-microphone switching.
4. Introduce a three-role snapshot and a narrow policy abstraction around enumeration/set calls so tests do not alter the reviewer's real defaults:

```csharp
public sealed record CaptureDefaults(string? Console, string? Multimedia, string? Communications);
// Capture -> apply roles -> rollback on partial failure -> retain snapshot for Restore.
```

5. Capture fresh defaults immediately before each successful switch cycle. Restore each role independently; report incomplete restoration accurately. Do not require a still-present virtual cable to reach the Restore branch.
6. Track the IDs this run applied. On actual exit, restore only roles still pointing to those IDs; preserve external user changes. Do not restore on ordinary Stop or hide-to-tray unless the user changes the proposed behavior.
7. Dispose enumerators/device wrappers correctly; do not retain temporary COM devices solely to get their IDs or labels.

Acceptance with fake endpoints: two installed cable families never cross-route; speakers disable Use; ambiguous mapping disables Use; distinct Console/Multimedia/Communications defaults round trip; failure on the second role rolls back the first; unplugged cable still allows Restore; external default change is preserved at exit. Then record a real reversible switch/restore check, with the original per-role state captured beforehand.

## Packet I — fresh LAN discovery and pre-Start handshake (user requirement)

Run after the safety fixes and before final integration acceptance. This is a bounded connection-lifecycle change, so split it into I1 and I2 and review the wire changes before installing either app.

Files: Android `MainActivity.kt`, `ControlChannel.kt`, `ControlProtocol.kt`, `MicConfig.kt`, `DiagnosticsLog.kt`, discovery/control tests; Windows `PocketMicEngine.cs`, `ControlProtocol.cs`, desktop `Program.cs`, control tests, `PROTOCOL.md`. A small dedicated Windows control-listener class is justified to avoid opening audio devices merely to answer discovery.

### Existing evidence to preserve

- Android already probes while idle. Do not add a second unmanaged probing loop.
- The toggle changes only a Boolean; its value is not a dependency of the existing control effect.
- Current adoption ignores `Found.audioPort`; the current probe target depends on the configured port.
- `ProbeNonceRing` has no reset method. Resetting a UI label alone leaves old response evidence eligible.
- Windows discovery is tied to audio engine Start/Stop. Current ANNOUNCE already echoes a nonce and uses a fixed 256-byte HMAC frame. Preserve that anti-replay and non-amplification behavior.

### I1 — explicit reset, freshness, and preliminary states

1. Add one `resetDiscovery`/`beginDiscovery` entry point that cancels the previous probe generation, clears peer, stats, last-stats time, nonce ring and bind error, and starts exactly one new attempt when enabled. Bind retries must own/close sockets and jobs coherently. Use a generation token in callbacks so a previous attempt cannot repopulate state after reset.
2. On Auto-connect OFF: cancel auto-discovery, clear derived target and readiness, show manual fields with no stale receiver, and retain the pairing key. On ON: clear derived address/port, publish Searching immediately and run a fresh LAN probe without Start. Keep capture-mode/gain/codec preferences.
3. Separate discovered target (nullable address + advertised port + identity + freshness) from manual-entry fields. Never turn a cleared port into an invalid config and then silently fall back to a stale default. Adopt address AND advertised audio port only after the appropriate authentication/capability checks.
4. Cancel/invalidate readiness on key change, port change, Wi-Fi/network change, receiver expiry, or a new discovery attempt. Add a receive timestamp/expiry to Found so it cannot stay ready forever after the PC disappears. Start must recheck the current generation's fresh result.
5. Publish preliminary states independent of microphone streaming:

```text
Idle/manual -> Searching -> Receiver found
 -> Pairing required or key mismatch
 -> Version/codec mismatch
 -> Receiver available, audio stopped (can handshake)
 -> Ready to stream (authenticated, compatible, receiver listening)
 -> Streaming (only after real microphone Start and delivery confirmation)
```

6. Preflight never opens AudioRecord, requests recording permission, acquires an audio wake lock, or starts `MicStreamingService`. Microphone permissions are evaluated only for the user's actual Start. Keep “receiver reachable” distinct from “audio delivered.”
7. Show a fresh nonce/HMAC result and actionable port/bind/network error. Do not claim an unknown firewall is the cause of timeout. Existing manual pairing/QR remains a fallback. A receiver with an unknown key can be displayed as an unverified candidate, never as authenticated/ready.
8. Start lightweight diagnostic state recording with connection discovery, not only after opening the diagnostics screen. Keep it bounded and exclude keys and audio payloads. Include generation, state, elapsed time and failure reason.

Suggested testable boundary (example, adapt names):

```kotlin
data class DiscoveredTarget(val host: String, val audioPort: Int, val generation: Long)
// UI emits SetAutoConnect(enabled); coordinator clears derived state and owns probe jobs.
// A fake clock + fake transport drives state tests without Android UI or microphone hardware.
```

### I2 — port-independent rendezvous and idle desktop responder

1. Extract/control the Windows discovery listener at application lifetime; Start/Stop audio only changes its readiness snapshot. Close discovery on actual application exit. Preserve one listener per socket; do not create two readers on the same UDP port.
2. Proposed concrete rendezvous: use the existing default control port 49501 for discovery, independent of the advertised audio port. Retain the session-control endpoint at advertised `audioPort + 1` for stats/config. When both are 49501, share one socket/dispatcher. Android probes the rendezvous and optionally the saved/manual control endpoint for old receivers; deduplicate replies by identity and generation.
3. Explicit collision rule: if configured audio itself is 49501, do not try to bind audio and discovery to that same port or silently change saved settings. Surface the conflict and offer another audio port or a clearly labeled legacy manual-only mode. Add this exception to both validation surfaces and protocol docs before release. Existing default 49500 remains unchanged.
4. The desktop reply advertises its configured audio port, authenticated protocol/capability information and whether audio is listening. Use the existing optional ANNOUNCE trailer pattern: append a small versioned capability/readiness extension after the current build-version bytes. Define exact offsets/lengths in both implementations and `PROTOCOL.md`, enforce the 214-byte payload maximum, and add shared byte fixtures. Old readers ignore the extension; old replies without it have unknown readiness/capabilities, never inferred ready.
5. Readiness cannot mean audio is usable simply because the control socket bound. Report audio bind/output-open errors and optional Opus availability separately. Do not advertise Opus support merely because this receiver parses v2 headers. No auto-start of capture as a side effect of probing.
6. Do not implement subnet-wide unicast sweeps, internet discovery, UPnP, firewall mutation, or credential broadcasting. Use the existing directed LAN broadcast strategy and a documented manual fallback.

### Mandatory tests and device scenarios

- Stale host -> OFF -> ON: host/peer/stats/nonces/readiness clear and exactly one fresh probe job starts.
- Late response from generation N cannot populate N+1 even if HMAC is valid.
- Idle phone with no microphone permission can find/preflight an idle desktop; no mic service exists and audio capture is not started.
- Receiver audio port differs from saved phone port: rendezvous finds the advertised port without manual IP/port edits.
- Empty/wrong pairing key finds a candidate but never Ready; scanning a valid QR re-handshakes and becomes Ready once desktop audio is listening.
- PC disappears while phone is idle: Ready expires within the defined timeout and Start does not rely on the stale result.
- Phone Wi-Fi changes: stale target clears; discovery uses the new subnet.
- Same/default control port uses one socket; non-default ports and 49501 collision have explicit tested outcomes.
- Old phone/new desktop and new phone/old desktop interoperate within known capabilities; absent new trailer means unknown preflight detail.
- Real-device receipt: launch both apps, leave microphone stopped, enable Auto-connect, observe preliminary state; then Start and verify separate delivery confirmation. Repeat with stale saved IP, wrong key, changed audio port, receiver restart, and OFF/ON.

Do not guess that the user's original manual-IP workaround was caused by only one of these issues. The captured live session proves a discovery key mismatch before successful audio; packet capture is needed to attribute earlier broadcast/port failures.

## Packet H — live export (D5), integration, and remaining controls

0. Fix live report export first. `SessionReportWriter.ReadSession` must read while `SessionRecorder` holds its writer open. Use an explicit `FileStream` with `FileAccess.Read` and `FileShare.ReadWrite`, then parse a bounded snapshot of complete newline-terminated records (or use a recorder snapshot under its write lock). Merely enabling sharing leaves a potential partially appended JSON tail: exclude only that incomplete trailing record, never swallow malformed interior records. Keep existing stopped-session behavior and friendly error handling. Test export with an open/appending recorder, a partial final line, valid interior lines, malformed interior data, cancel, and unwritable output. No need to stop the receiver to export.

1. Fix the pre-existing Android null-error assertion after checking its contract. Rerun complete Windows and Android suites and desktop/native builds; record actual totals, not a hard-coded expectation of 146 after adding tests.
2. Complete every row of the control matrix in `DESKTOP-CONTROL-REVIEW.md`. Record PASS / FAIL / UNVERIFIED, build SHA, scenario, and evidence. A control with only a source trace remains unverified at runtime.
3. Exercise invalid port/key dialogs, occupied port startup, absent `opus.dll`, monitor-open failure, malformed packet recovery, no-session/export/save/cancel, automatic startup, Basic/Advanced visibility, clipboard, QR, tray Show/Exit, and actual socket release.
4. QR test keys must be synthetic. Include reserved URI characters and Unicode and inspect `parseQrPairingData` in `QrScanner.kt` before choosing URI escaping; fix only if a failing round trip proves the mismatch.
5. Verify all controls remain reachable at minimum size and 100/125/150/200% display scaling by keyboard and scrolling. Do not start a visual redesign.
6. Physical Android -> Windows PCM and Opus, VB-CABLE -> a target app, monitor audio on/off/device changes, screen-lock, and timed loss/drift checks need actual device/network receipts. If unavailable, report UNVERIFIED and provide exact manual steps; do not simulate a hardware pass.
7. Reconcile README/PROTOCOL claims only after behavior is established: codec reachability, FEC versus PLC, active/manual buffering, optional-native fallback. Leave publishing/release work for a separate explicit request.

## Completion report template

```text
Base/current SHA:
Packets completed: A B C D E F G I1 I2 H (explicitly list incomplete items)
Changed production files and purpose:
Regression tests added and exact results:
Desktop build result:
Android JVM/lint/native build results:
Control matrix: passed / failed / unverified counts, with row evidence
Hardware receipts:
Remaining risks and next concrete check:
Independent reviewer findings:
No commit/push/release performed unless separately authorized.
```

Do not treat this plan as a completed implementation. Prefer a small reviewed change per packet; if packets must share a test seam, introduce only the seam needed for the real regression and preserve existing tests.
