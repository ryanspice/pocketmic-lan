# LinkQualityPolicy Integration Plan

## Overview

Wire `LinkQualityPolicy` into `PocketMicEngine` and the WinForms UI so the receiver
auto-tunes its jitter buffer and concealment window based on measured link quality,
while always respecting the user's slider bounds and surfacing the current tier and
reason in the diagnostics panel.

---

## 1. Engine Changes — `PocketMicEngine.cs`

### 1.1 New field (after line ~133, near the other private fields)

```csharp
private readonly LinkQualityPolicy _linkPolicy = new();
private LinkAssessment? _lastAssessment;
```

`_linkPolicy` lives for the entire engine lifetime (not per-run), so its hysteresis
state carries across stop/start cycles. This is intentional — if a session was running
on a bad link and is restarted, the policy should remember that the link was bad rather
than starting fresh at Good.

`_lastAssessment` caches the most recent `LinkAssessment` so it can be read by new
events without re-evaluating.

### 1.2 New public properties

```csharp
/// <summary>
/// The link quality policy's current assessment. Null until the first 10-second
/// window closes. Updated by the analytics window callback, not the receive loop.
/// </summary>
public LinkAssessment? CurrentLinkAssessment => _lastAssessment;
```

### 1.3 New event (after `AnalyticsWindowClosed`, ~line 183)

```csharp
/// <summary>
/// Raised after the link quality policy evaluates a new 10-second window.
/// The assessment contains the tier, recommended prebuffer, concealment, action, and reason.
/// Hosts subscribe to this rather than polling CurrentLinkAssessment.
/// </summary>
public event EventHandler<LinkAssessment>? LinkQualityChanged;
```

### 1.4 Hook into the analytics window callback (`StartAnalytics`, line ~450)

The existing code in `StartAnalytics` subscribes to `collector.WindowClosed`:

```csharp
collector.WindowClosed += window =>
{
    recorder?.AppendWindow(window);
    AnalyticsWindowClosed?.Invoke(this, window);
};
```

**Change this to:**

```csharp
collector.WindowClosed += window =>
{
    recorder?.AppendWindow(window);

    // Evaluate link quality on every 10-second window. The "session" and "1m"
    // windows are informational summaries — policy decisions should only react
    // to the most recent 10-second window to keep response time tight.
    if (window.WindowKind == "10s")
    {
        var assessment = _linkPolicy.Evaluate(
            window,
            _bufferSliderMin,
            _bufferSliderMax);

        _lastAssessment = assessment;

        // Apply the recommended prebuffer if it differs from the current value.
        // The policy already clamped the value to user bounds, so this is safe
        // to apply unconditionally.
        if (assessment.Prebuffer != _prebufferMilliseconds)
        {
            _prebufferMilliseconds = assessment.Prebuffer;
            _highWaterMilliseconds = AudioPipeline.HighWaterFor(assessment.Prebuffer);
        }

        LinkQualityChanged?.Invoke(this, assessment);

        // Log the policy decision to the session recorder.
        _analytics?.RecordDiscoveryEvent(
            "link-quality",
            $"tier={assessment.Tier} action={assessment.Action} " +
            $"prebuffer={assessment.Prebuffer}ms concealment={assessment.ConcealmentPackets}pkts " +
            $"reason={assessment.Reason}");
    }

    AnalyticsWindowClosed?.Invoke(this, window);
};
```

**Why only "10s" windows?** The 10-second window is the policy's designed input cadence.
The "1m" and "session" windows are cumulative summaries that would provide stale data
to the policy. The policy already has its own multi-window hysteresis (3 consecutive
good windows for promotion), so reacting to every 10s window gives it the right
temporal resolution.

### 1.5 User slider bounds (new private fields, ~line 133)

The policy's `Evaluate()` needs `userPrebufferMin` and `userPrebufferMax`. These come
from the UI slider (40–300 ms), but the engine itself doesn't know about the slider.
Two options:

**Option A (Recommended):** Add bounds as constructor/property values on the engine.

```csharp
/// <summary>
/// Lower bound for the link quality policy's prebuffer recommendations.
/// Matches the UI slider minimum (40 ms). The policy never recommends below this.
/// </summary>
private const int LinkPolicyPrebufferMin = 40;

/// <summary>
/// Upper bound for the link quality policy's prebuffer recommendations.
/// Matches the UI slider maximum (300 ms). The policy never recommends above this.
/// </summary>
private const int LinkPolicyPrebufferMax = 300;
```

These are `const` because the slider bounds (40–300) are fixed at compile time and
the policy's design documentation explicitly says "the UI slider sets bounds."
If the slider range changes, these constants must change in lockstep.

Used in the `Evaluate()` call: `_linkPolicy.Evaluate(window, LinkPolicyPrebufferMin, LinkPolicyPrebufferMax)`.

### 1.6 Concealment integration in the receive loop (`ReceiveLoopAsync`, line ~1002)

Currently the receive loop hard-codes `AudioPipeline.MaxConcealedGapPackets` (20):

```csharp
if (delta <= AudioPipeline.MaxConcealedGapPackets)
{
    for (var index = 0; index < delta; index++)
    {
        AudioPipeline.BuildConcealmentFrame(conceal, lastGood, haveLastGood, index);
        _audioBuffer?.AddSamples(conceal, 0, conceal.Length);
    }
}
```

**Change to read the recommended concealment from the policy:**

```csharp
var maxConcealed = _lastAssessment?.ConcealmentPackets
    ?? AudioPipeline.MaxConcealedGapPackets;

if (delta <= maxConcealed)
{
    for (var index = 0; index < delta; index++)
    {
        AudioPipeline.BuildConcealmentFrame(conceal, lastGood, haveLastGood, index);
        _audioBuffer?.AddSamples(conceal, 0, conceal.Length);
    }
}
```

**Why read from `_lastAssessment` rather than the policy directly?** The receive loop
runs on a hot path and must not allocate or take locks. `_lastAssessment` is a `record`
reference swap — a single atomic pointer read. Reading the policy directly would require
either a lock or making its fields volatile, neither of which is necessary here.

**When `_lastAssessment` is null (first 10 seconds of a run):** Falls back to the
existing `MaxConcealedGapPackets` constant (20 packets), which is the current Good-tier
default. This is correct because the policy defaults to Good tier.

### 1.7 Reset on start (`Start`, line ~320)

After `StartAnalytics(options)`, add:

```csharp
// Reset the link policy's hysteresis state for a fresh run, so a previous
// session's bad link doesn't carry over into a new start.
// (Comment: we intentionally do NOT reset _linkPolicy itself — only the
// assessment cache. The policy's hysteresis across runs is a feature.)
_lastAssessment = null;
```

Wait — re-reading the design: the policy comment says "Respect user overrides" and
"Never fight the user." The policy's own `Reset()` method exists for exactly this case.
On each new `Start()`, call:

```csharp
_linkPolicy.Reset();
_lastAssessment = null;
```

This is the right behavior: each new session starts fresh at Good tier.

### 1.8 Phone signal on Poor tier demotion (optional, Phase 2)

When `assessment.ShouldSignalDemotion` is true (tier drops to Poor), the engine
should signal the phone to switch to a more aggressive Wi-Fi lock mode. This requires:

1. **New control message type** — Add `TypeSignal = 5` to `ControlProtocol.cs` with a
   single-byte payload indicating the signal kind (e.g., `0x01` = "switch to
   HIGH_PERFORMANCE Wi-Fi lock").

2. **New method on PocketMicEngine** — `SendSignalToPhone(byte signalKind)` that builds
   and sends a `TypeSignal` frame on the control channel, using the same
   `_controlKey`/`_control` infrastructure.

3. **Android-side handler** — `ControlChannel.kt` needs to parse `TypeSignal` frames
   and call the streaming service's wifi lock manager to switch modes.

This is a cross-platform change that should be done as a separate PR after the core
integration is verified. The `ShouldSignalDemotion` flag is already set by the policy;
we just need the plumbing to act on it.

---

## 2. UI Changes — `windows-receiver/Program.cs` (MainForm)

### 2.1 New UI control for link quality status

Add a label in the diagnostics section (after `_statsLabel`, around line 203):

```csharp
private readonly Label _linkQualityLabel = new()
{
    Text = "Link: —",
    AutoSize = true,
    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
    ForeColor = Color.DarkGreen,
};
```

### 2.2 Layout placement

In the constructor, add `_linkQualityLabel` to the status panel, right after
`_statsLabel` (around line 344):

```csharp
statusPanel.Controls.Add(_statsLabel);
statusPanel.Controls.Add(_linkQualityLabel);  // NEW
```

### 2.3 Subscribe to `LinkQualityChanged` (in `SubscribeToEngine`, after line ~667)

```csharp
_engine.LinkQualityChanged += (_, assessment) =>
{
    var runId = _runId;
    var tierText = assessment.Tier.ToString();
    var actionText = assessment.Action switch
    {
        LinkAction.Promote => "↑",
        LinkAction.Demote => "↓",
        LinkAction.Hold => "—",
        _ => "",
    };
    var color = assessment.Tier switch
    {
        LinkQualityPolicy.LinkTier.Excellent => Color.DarkGreen,
        LinkQualityPolicy.LinkTier.Good => Color.DarkGreen,
        LinkQualityPolicy.LinkTier.Degraded => Color.DarkOrange,
        LinkQualityPolicy.LinkTier.Poor => Color.Firebrick,
        _ => SystemColors.ControlText,
    };
    var text = $"Link: {tierText} {actionText}  " +
               $"buffer {assessment.Prebuffer} ms  " +
               $"conceal {assessment.ConcealmentPackets}pkts  " +
               $"— {assessment.Reason}";

    PostUi(() =>
    {
        if (runId != _runId || !_engine.IsRunning) return;
        _linkQualityLabel.Text = text;
        _linkQualityLabel.ForeColor = color;
    });
};
```

### 2.4 Clear on stop (in `StopReceiverCoreAsync`, after line ~1031)

```csharp
_linkQualityLabel.Text = "Link: —";
_linkQualityLabel.ForeColor = SystemColors.ControlText;
```

### 2.5 Buffer label enrichment (optional, in `ApplyBufferSetting`)

When the link quality policy is active, the buffer label could show whether the
current prebuffer is user-set or policy-set. This is informational:

In `ApplyBufferSetting()` (line ~689), after computing `descriptor`, the label
already shows the prebuffer value. No change needed here — the `_linkQualityLabel`
in the status panel already shows the policy's recommended value and the reason,
which is more informative than a label tweak.

---

## 3. AnalyticsPanel Changes — `AnalyticsPanel.cs`

### 3.1 Show link quality tier in the header

The `AnalyticsPanel.DrawHeader` method (line ~115) renders a summary line. Add
the link quality tier to this summary.

**New field** (after `_latestSession`, line ~35):

```csharp
private LinkAssessment? _latestAssessment;
```

**New method** (after `ResetHistory`, line ~72):

```csharp
/// <summary>
/// Receives a link quality assessment from the engine. Call from the UI thread,
/// same as PushWindow.
/// </summary>
public void PushLinkAssessment(LinkAssessment assessment)
{
    _latestAssessment = assessment;
    Invalidate();
}

/// <summary>Clears assessment history without disposing the control.</summary>
public void ResetLinkQuality()
{
    _latestAssessment = null;
}
```

**Modify `DrawHeader`** (line ~127) to include the tier:

```csharp
var tierSuffix = _latestAssessment is { } la
    ? $"   ·   Link {la.Tier} ({la.Action})"
    : "";

var summary =
    $"Loss {current.LossPercent:F2}%   ·   " +
    $"p50/p95/p99 {current.InterarrivalP50Ms:F0}/{current.InterarrivalP95Ms:F0}/{current.InterarrivalP99Ms:F0} ms   ·   " +
    $"Buffer {current.BufferDepthAvgMs:F0} ms   ·   " +
    $"Level {current.LevelDbfsAvg:F1} dBFS   ·   " +
    $"Gate {current.NoiseGateActivePercent:F0}%" +
    tierSuffix;
```

### 3.2 Wire it up from MainForm

In `SubscribeToEngine`, add alongside the `AnalyticsWindowClosed` handler:

```csharp
_engine.LinkQualityChanged += (_, assessment) =>
{
    var runId = _runId;
    PostUi(() =>
    {
        if (runId != _runId) return;
        _analyticsPanel.PushLinkAssessment(assessment);
    });
};
```

In `StartReceiverAsync`, after `_analyticsPanel.ResetHistory()`:

```csharp
_analyticsPanel.ResetLinkQuality();
```

---

## 4. File-by-File Change Summary

| File | Change | Lines Affected |
|------|--------|----------------|
| `windows-receiver-core/PocketMicEngine.cs` | Add `_linkPolicy`, `_lastAssessment` fields | ~133 |
| `windows-receiver-core/PocketMicEngine.cs` | Add `CurrentLinkAssessment` property | ~185 |
| `windows-receiver-core/PocketMicEngine.cs` | Add `LinkQualityChanged` event | ~183 |
| `windows-receiver-core/PocketMicEngine.cs` | Add `LinkPolicyPrebufferMin/Max` constants | ~133 |
| `windows-receiver-core/PocketMicEngine.cs` | Hook policy evaluation into `StartAnalytics` WindowClosed callback | ~450 |
| `windows-receiver-core/PocketMicEngine.cs` | Use `_lastAssessment?.ConcealmentPackets` in receive loop | ~1002 |
| `windows-receiver-core/PocketMicEngine.cs` | Reset `_linkPolicy` and `_lastAssessment` in `Start` | ~320 |
| `windows-receiver/Program.cs` | Add `_linkQualityLabel` field | ~203 |
| `windows-receiver/Program.cs` | Add label to status panel layout | ~344 |
| `windows-receiver/Program.cs` | Subscribe to `LinkQualityChanged` in `SubscribeToEngine` | ~667 |
| `windows-receiver/Program.cs` | Clear label in `StopReceiverCoreAsync` | ~1031 |
| `windows-receiver/Analytics/AnalyticsPanel.cs` | Add `_latestAssessment` field, `PushLinkAssessment`, `ResetLinkQuality` | ~35, ~72 |
| `windows-receiver/Analytics/AnalyticsPanel.cs` | Include tier in `DrawHeader` summary | ~127 |

---

## 5. Data Flow (End-to-End)

```
Receive Loop (100 packets/s)
  │
  ├─ RecordDelivered/RecordLost/etc → AnalyticsCollector queue
  │
  └─ Uses _lastAssessment?.ConcealmentPackets for gap concealment

Analytics Background Thread (every 100ms drain)
  │
  └─ CheckRollovers() → Build("10s") → WindowClosed event
       │
       └─ PocketMicEngine.StartAnalytics callback (analytics thread)
            │
            ├─ _linkPolicy.Evaluate(window, 40, 300) → LinkAssessment
            │    ├─ Updates _lastAssessment
            │    ├─ Applies recommended prebuffer to _prebufferMilliseconds
            │    │   (which the receive loop reads for buffer decisions)
            │    └─ Logs to session recorder via RecordDiscoveryEvent
            │
            ├─ LinkQualityChanged?.Invoke(assessment)
            │    │
            │    └─ MainForm handler → PostUi → _linkQualityLabel.Text
            │
            └─ AnalyticsWindowClosed?.Invoke(window)
                 │
                 └─ MainForm handler → PostUi → _analyticsPanel.PushWindow(window)
                      └─ AnalyticsPanel.PushLinkAssessment(assessment) (new)
                           └─ DrawHeader includes tier in summary
```

---

## 6. Thread Safety Analysis

| Access Pattern | Thread | Safety Mechanism |
|----------------|--------|-----------------|
| `_linkPolicy.Evaluate()` | Analytics background thread | Only called from `WindowClosed` callback, which fires on the analytics thread. No concurrent access to the policy. |
| `_lastAssessment` read by receive loop | Audio thread | Record reference read — atomic on .NET. The old assessment is never mutated, only replaced with a new record. |
| `_prebufferMilliseconds` set by policy | Analytics thread | Written by policy callback, read by receive loop. Both are `int` — a torn read on 32-bit would yield an intermediate value, but the field is aligned and the worst case is one packet using a slightly stale prebuffer. Acceptable. Alternatively, make it `volatile` for correctness. |
| `_linkQualityLabel.Text` set by event handler | UI thread (via `PostUi`) | Marshalled through `BeginInvoke`, same as every other UI update. |
| `_analyticsPanel.PushLinkAssessment()` | UI thread (via `PostUi`) | Same pattern as `PushWindow`. |

**Recommendation:** Mark `_prebufferMilliseconds` as `volatile` or use `Interlocked`
for the field that crosses the analytics-thread-to-audio-thread boundary. Currently
it's a plain `int` field, but it's already written from the UI thread (via
`PrebufferMilliseconds` setter) and read from the audio thread without synchronization.
The existing code relies on the fact that `int` reads are atomic on .NET, which is
guaranteed by the runtime spec. Adding the policy write from a third thread is fine
under the same guarantee.

---

## 7. Testing Strategy

### 7.1 Existing tests remain valid
- `LinkQualityPolicyTests.cs` (13 tests) — already pass and cover the policy logic.
- No policy logic changes in this integration.

### 7.2 New integration tests to add

| Test | What it verifies |
|------|-----------------|
| `Engine_applies_policy_prebuffer_after_first_window` | After the first 10s window closes, `_prebufferMilliseconds` matches the policy's recommendation. |
| `Engine_respects_user_slider_bounds` | If the user slider is at 80ms and the policy recommends 40ms (Excellent), the applied value is 80, not 40. |
| `Engine_resets_policy_on_restart` | After stop/start, the policy is back at Good tier. |
| `Engine_concealment_uses_policy_value` | After a demotion to Poor, the receive loop conceals up to 20 packets (the Poor tier value). |
| `UI_shows_tier_in_status_panel` | The `_linkQualityLabel` text contains "Poor" after a Poor-tier assessment. |
| `AnalyticsPanel_includes_tier_in_header` | The panel's header string contains the tier name. |

### 7.3 Manual testing

1. Start receiver, confirm "Link: Good —" appears in the status panel.
2. Deliberately degrade the link (add packet loss via `clumsy` or `tc` on a Linux
   bridge) — confirm the label changes to "Link: Degraded ↓" or "Link: Poor ↓".
3. Restore the link — confirm promotion back to Good after 30 seconds (3 windows).
4. Verify the buffer label reflects the policy's recommended value, not the slider value.
5. Move the slider to 200ms — confirm the policy cannot recommend below 200ms.

---

## 8. Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Policy oscillation between tiers | Already handled by hysteresis (3 windows for promotion, 1 for demotion). The `PrebufferStepUp`/`PrebufferStepDown` smoothing prevents jitter in the applied value. |
| Policy fights the user's slider | The `Math.Clamp` in `Evaluate()` ensures the recommendation never goes outside [slider min, slider max]. If the user sets 200ms, the policy operates in [200, 300]. |
| Session log becomes noisy | The "link-quality" discovery event fires every 10s — same cadence as the analytics window. This matches existing discovery event density (probe events also fire on discovery). |
| Phone signal (Phase 2) requires Android changes | The `ShouldSignalDemotion` flag is set but not acted on until the Android handler is wired. No harm in setting it early. |
| First 10 seconds have no policy data | `_lastAssessment` is null; concealment falls back to `MaxConcealedGapPackets` (20), prebuffer stays at user setting. This is correct — the policy needs a full window of data before it can assess. |

---

## 9. Implementation Order

1. **Engine fields + constants** (1.1, 1.2, 1.3, 1.5) — pure additions, no behavior change.
2. **Engine policy hook** (1.4) — the core integration. Apply prebuffer and log events.
3. **Engine concealment** (1.6) — use `_lastAssessment` in the receive loop.
4. **Engine reset** (1.7) — reset on start.
5. **UI label** (2.1–2.4) — display the tier/reason.
6. **AnalyticsPanel** (3.1–3.2) — show tier in diagnostics header.
7. **Tests** (7.2) — integration tests for the engine and UI.
8. **Phase 2: Phone signal** (1.8) — separate PR, requires Android changes.
