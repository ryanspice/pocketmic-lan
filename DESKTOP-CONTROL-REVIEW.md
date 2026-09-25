# Desktop control review — 2026-09-24

Reviewed source: `ddc8f38` (clean before review). This extends the audio/protocol review begun from `REVIEW-HANDOFF-ASTRA.md`. It reviews the current WinForms application, its shared engine, settings, routing helper, and diagnostics. The handoff's historical build/test claims are not treated as current proof.

**Result: the controls are not all working correctly.** Five desktop findings follow. See `LUNA-IMPLEMENTATION-HANDOFF.md` for the proposed implementation sequence, including the five preceding audio/protocol findings.

## Findings

### D1 — P1: the buffer slider does not control the running buffer

- `windows-receiver/Program.cs:714-729`, `ApplyBufferSetting`, assigns the slider directly to `PrebufferMilliseconds` and displays/persists that value.
- `windows-receiver-core/PocketMicEngine.cs:1144-1152` overwrites it on every accepted packet with the adaptive target. The desktop never calls `SetBufferBounds`.
- `LinkQualityPolicy.Evaluate`'s insufficient-data return omits `TierPrebufferMin` and `TierPrebufferMax`, leaving both zero. The engine copies these bounds at lines 540-541, so a quiet window can make the next effective prebuffer zero.
- Reproduced against the built engine with two authenticated synthetic packets and no audio device: requested 200 ms -> 100 ms on the first packet; applying the zero bounds returned by the insufficient-data branch -> 0 ms on the next packet.
- In the running desktop, the label remained “Buffer 200 ms” while the link recommendation displayed 100 ms. Neither label is an authoritative display of the effective target.

Required correction: make automatic versus manual ownership explicit, preserve valid bounds on every assessment path, and display the actual applied target separately from recommendations.

### D2 — P2: Monitor through remains editable but changes do not reach the engine

- `Program.cs:565-572` handles the monitor checkbox, but no handler exists for `_monitorCombo.SelectedIndexChanged`.
- Start captures `MonitorDeviceNumber` once (`Program.cs:995-1002`). `PocketMicEngine.MonitorEnabled` reopens monitoring using `_currentOptions` from that start (`PocketMicEngine.cs:661-666`).
- Changing the combo while running therefore leaves the old output active; toggling monitoring off/on still uses that old output. `MonitorStarted` then persists the currently displayed combo value, potentially saving a device different from the one actually opened.
- Runtime: monitor checkbox enabled the combo; choices were enumerated. A dropdown click was blocked by the desktop automation window guard, so a live device switch was not successfully exercised. The missing handler and stale options are source-confirmed.

Required correction: either apply selected monitor devices immediately and persist only successful opens, or disable the selector while running. The proposed handoff chooses immediate application because the checkbox is already live.

### D3 — P2: desktop Strength stops affecting DSP after phone Custom settings

- `VoiceProcessor.Apply` sets `UseCustom=true` (`windows-receiver-core/VoiceProcessor.cs:79-84`). Effective gate/compressor/makeup then ignore `Strength`.
- `Program.cs:782-801` changes `VoiceStrength` and shows a new percentage but never exits Custom mode.
- Reproduced in the built processor: apply Custom gate=0.2/compressor=0.3, set Strength=0.9; effective values remain 0.2/0.3 and `UseCustom` remains true.
- Runtime: an authenticated synthetic control CONFIG changed the desktop label to Custom, confirming the phone-to-desktop event route. Post-Custom slider audio response was verified at processor level, not by listening.

Required correction: a deliberate desktop strength adjustment must explicitly select preset processing (and reset preset filter parameters), or the slider must be disabled in Custom mode with an explicit return-to-preset action. The handoff chooses desktop adjustment as a preset override and suppresses feedback while reflecting phone updates.

### D4 — P1: “Use PocketMic as Windows microphone” can choose an unrelated cable

- `Program.cs:448-450` ignores the selected playback device and calls `FindVirtualCaptureDevice()` globally.
- `DefaultDeviceSwitcher.cs:80-108` chooses the first matching capture hint, so a receiver playing to VoiceMeeter can switch Windows capture to VB-CABLE when both are installed.
- `_outputCombo` has no selection-change handler. The routing explanation and button eligibility can remain from the initial cable after selecting speakers. `DescribeRouting` also does not clear button eligibility in its non-cable branch.
- The control can therefore announce that apps hear the phone while selecting an endpoint with no corresponding receiver audio.
- Source-confirmed. System microphone defaults were not changed during this review; end-to-end cable routing is not claimed as passed.

Required correction: resolve the capture endpoint from the selected render endpoint, refresh routing state on selection, and disable the action for speakers, missing endpoints, or ambiguous mappings.

The same Use/Restore control also needs correct restoration:

- `DefaultDeviceSwitcher.CurrentDefaultCaptureId` remembers only the Communications role (`DefaultDeviceSwitcher.cs:116-124`).
- `TrySetDefaultCapture` changes Console, Multimedia, and Communications (`DefaultDeviceSwitcher.cs:156-166`). Restore assigns that single saved ID to all three roles.
- A user with separate normal and communications microphones loses the original role split even after pressing Restore. A failure after the first role changes also leaves partial mutation without rollback.
- Source-confirmed. Live default-device mutation/restore was not exercised.

Required correction for D4: snapshot each role, roll back partial application, and restore each role independently. Restore must remain available even if the selected cable has disappeared. Do not silently overwrite a user's later external default-device change.

### D5 — P2: Export session report fails during a running session

- Reproduced by clicking Export while real phone packets were arriving, accepting the proposed new Markdown filename, and observing “Could not write the session report” with a sharing-violation error on the active JSONL source file.
- `SessionRecorder.cs:47` keeps a write handle open with `FileShare.Read`; `SessionReportWriter.cs:37` reads with `File.ReadLines`, whose read sharing does not permit the already-open writer.
- After clicking Stop, generating the same session's Markdown succeeded (9,568 characters). Stop also released both audio/control ports and re-enabled connection inputs; Start resumed reception from the phone.

Required correction: read a bounded, complete-line snapshot with appropriate read/write sharing, or obtain a synchronized snapshot from the recorder. Preserve cancellation and error reporting, and test export while a recorder is actively appending.

## Control coverage and limits

“Source traced” means the visible control's handler and downstream path were inspected; it is not a successful live interaction or hardware receipt.

| Control / behavior | Evidence in this review | Remaining acceptance |
|---|---|---|
| Start receiver | Clicked; entered Listening; accepted synthetic authenticated PCM over loopback and authenticated traffic from the physical Android phone | Audible output and verification with an APK built from this checkout |
| Stop receiver | Clicked; Stopped state, connection inputs re-enabled, both UDP ports released; Start resumed phone reception | Native shutdown race regression tests |
| UDP port | Disabled after Start; shared range validation traced | Invalid values, occupied audio/control ports, persistence |
| Pairing key | Synthetic key used; field disabled during run | Invalid-length dialog and wrong-key recovery |
| Playback output | Device list populated; disabled during run | Selection/start persistence, hot unplug, D4 |
| Monitor checkbox | Clicked; monitor selector enabled | Audio on/off receipt and failed-open feedback |
| Monitor through | Choices enumerated; source defect D2 | Live change, restart persistence, failure rollback |
| Buffer slider | Engine reproduction confirms D1 | Manual/automatic behavior and displayed target |
| Voice enhancement | Clicked off/on; strength disabled/enabled and labels changed; restored enabled state | Bypass audio equivalence and restart persistence |
| Strength | Processor reproduction confirms D3 | UI override, phone update feedback, preset filter reset |
| Use/Restore Windows microphone | Source defect D4, including restoration behavior | Selected cable mapping, per-role restore, partial failure |
| Auto-listen | Synthetic false preference respected at launch | True preference on restart and startup failure |
| Close to tray | Handler and tray menu source traced | X while streaming, Show/double-click, Exit and port release |
| Advanced mode | Basic/Advanced toggled; advanced controls disappeared/reappeared; returned to Advanced | Keyboard reachability and restart persistence |
| Copy first IP | Handler/source traced | Clipboard content under multiple/no adapters |
| Show QR | Synthetic pairing modal observed | Physical scan, delimiter/unicode key round trip, screen fit |
| Export session report | Save dialog opened; live export FAILED with source-log sharing violation; stopped-session generation succeeded (D5) | Fix then retest live save, cancel, no-session and unwritable paths |
| Diagnostics | Loopback stats and link label observed | Live export contents, graph progression, final flush |
| Window maximize | Clicked successfully | Minimum size, 125–200% DPI, scroll/keyboard access |
| Minimize/restore/close | Normal Alt+F4 exit with close-to-tray disabled completed; process disappearance verified; relaunched with saved settings | Tray/taskbar behavior and exit race coverage |

## Executed checks

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build windows-receiver/PocketMicReceiver.csproj --no-restore --verbosity minimal
& 'C:\Program Files\dotnet\dotnet.exe' test windows-receiver-tests/PocketMicReceiver.Tests.csproj --no-restore --verbosity minimal
```

- Desktop build: PASS, 0 warnings and 0 errors.
- Windows tests: PASS, 146 passed, 0 skipped, 0 failed (rerun in this review).
- In-memory engine/processor probes: reproduced buffer overwrite/zero clamp and Custom-strength behavior. No production source was modified for the probes.
- Initial desktop probe used a synthetic pairing key and alternate port, retaining original settings in memory. The user subsequently changed pairing settings and connected their phone. Those deliberate user changes are preserved rather than overwritten by the pre-review snapshot. No phone capture was started by the synthetic probe; its PCM was digital silence.
- 240 authenticated loopback packets were counted after the UI's throttled counter refreshed. This proves receive/control plumbing, not audibility or phone capture.
- Desktop automation detected other input during testing. No successful monitor device change is inferred from the interrupted interaction.
- Prior review's Android JVM result remains 78 passed / 1 failed (`StreamingStateTest.resetReturnsToIdleDefaults`, expected empty string versus actual null). Android tests were not rerun for this desktop-only review.
- Native default-device changes, clipboard content, physical QR scanning, long-run audio, screen-lock, and hardware latency remain unverified.

## User-assisted live diagnostics and discovery requirement

The user confirmed they were interacting with the app, authorized continuation, and requested that Auto-connect toggling clear stale targets, rediscover the LAN, and perform preliminary handshakes before Start. This is recorded as new implementation scope in packet I of the Luna handoff.

- ADB recognized the connected OnePlus 9 Pro (LE2125), installed package v0.1.5/versionCode 5, last updated September 11. A foreground microphone service was active. The installed notification ID was 49500, whereas current source uses 10001: the running APK must not be described as a verified build of this checkout.
- Read-only phone UI inspection reported Connected to the PC; the PC reported Receiving audio from the physical phone.
- Session `session_20260924_042943.jsonl` first recorded a discovery probe/key mismatch at 04:29:43 local time, then authenticated phone traffic at 04:30:06. This establishes a pairing mismatch during setup; it does not prove why every earlier discovery attempt failed.
- A one-minute window ending 04:35:43 recorded 6,006 packets, zero loss/rejection, 253 trims, interarrival P95/P99 11/14 ms, and average queue depth 152.27 ms. Whole-session counters include setup failures and must not be confused with current-window health.
- No PocketMic warnings/AndroidRuntime errors were returned by the filtered logcat query for the current process. The in-app DiagnosticsLog is memory-only and begins observing when its screen is opened; empty logcat is not proof that no prior issue occurred.
- The current source already initiates idle probing (`MainActivity.kt:166-170`), but the Auto-connect switch (`331-333`) merely changes/persists a Boolean. It does not clear host/peer/stats/nonces or force a new discovery generation. Discovered adoption copies address but not `found.audioPort` (`176-182`). Probes target only the configured audio-port-plus-one, so an unknown non-default receiver port cannot be learned from a broadcast sent elsewhere.
- The desktop control listener currently starts/stops with the audio engine. Preflight while both sides are stopped needs an explicit discovery lifecycle, not just a label change.

The real stream improves transport evidence but does not establish Opus use, audible VB-CABLE delivery, calibrated input levels, or end-to-end latency. The first Stop/Start check resumed phone reception.

Cleanup included a normal desktop exit/relaunch to clear the synthetic Custom DSP configuration used by the probe, while preserving the user's updated pairing settings. This also exercised normal close with close-to-tray disabled. The receiver was restarted and left Listening on the user's current port; the final ADB service check showed phone capture stopped, and the new desktop session showed discovery probes but no audio packets. Phone Start is needed to resume capture. No driver/default-microphone change was made.

## Delivery boundary

This document records the 2026-09-24 review snapshot; later source changes do not alter what was observed during that review. Follow-up implementation now addresses D1 (explicit automatic/manual buffer ownership and valid fallback bounds), D2 (transactional live monitor-output switching), D3 (desktop Strength leaves phone Custom mode and restores preset filters), D4 (exact selected-cable pairing, per-role snapshots, rollback, explicit restore, and ownership-aware exit restore), and D5 (exporting a complete-line report snapshot while the session recorder remains open). The Windows suite passes 173 tests and the desktop build passes with 0 warnings/errors for the current D5 working tree. D4 hosted CI is green. D5 hosted CI and the running-app Save-dialog retest are pending; D1-D4 live Windows interaction/audio acceptance also remains open. Attach a result to each acceptance row; any hardware-dependent row without a receipt stays unverified.
