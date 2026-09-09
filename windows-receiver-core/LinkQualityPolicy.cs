namespace PocketMicReceiver;

/// <summary>
/// Adaptive link quality policy that auto-tunes the receiver's latency/robustness trade
/// based on measured connection health from the 10-second analytics window.
///
/// The receiver already tracks everything needed: loss%, interarrival jitter percentiles,
/// buffer depth, and packet rate. This policy evaluates those metrics and classifies the
/// link into quality tiers, then recommends buffer adjustments.
///
/// Design principles:
/// - Promote slowly (sustained good conditions over multiple windows)
/// - Demote immediately (bad conditions need instant response)
/// - Never oscillate (hysteresis on both entry and exit thresholds)
/// - Respect user overrides (the UI slider sets bounds, this policy operates within them)
/// - Never fight the user (if the user manually set 200ms, don't promote below 200ms)
///
/// The policy produces a recommended prebuffer value. The engine applies it if and only
/// if it falls within the user's configured range. The user's slider becomes a "bounds"
/// rather than an exact setting — the policy operates within those bounds.
/// </summary>
public sealed class LinkQualityPolicy
{
    /// <summary>
    /// Link quality tiers, from best to worst. Each tier maps to a recommended prebuffer
    /// range and concealment window. The names are intentionally descriptive — they appear
    /// in diagnostics and session reports.
    /// </summary>
    public enum LinkTier
    {
        /// <summary>
        /// Jitter p99 ≤ 20ms, loss &lt; 0.5%, no tail spikes. Safe to minimize latency.
        /// Prebuffer: 40-60ms. Concealment: 5 packets (50ms).
        /// </summary>
        Excellent,

        /// <summary>
        /// Jitter p99 ≤ 40ms, loss &lt; 1.5%, occasional spikes. Current settings hold.
        /// Prebuffer: 60-120ms. Concealment: 10 packets (100ms).
        /// </summary>
        Good,

        /// <summary>
        /// Jitter p99 ≤ 80ms or loss &lt; 3%. Wider buffers needed.
        /// Prebuffer: 120-200ms. Concealment: 15 packets (150ms).
        /// </summary>
        Degraded,

        /// <summary>
        /// Jitter p99 &gt; 80ms or loss ≥ 3% or tail spikes &gt; 200ms. Emergency mode.
        /// Prebuffer: 200-300ms. Concealment: 20 packets (200ms).
        /// Signal the phone to switch Wi-Fi lock mode.
        /// </summary>
        Poor,
    }

    // --- Thresholds (entry conditions) ---
    // Promotion uses the LOWER bound of the next tier.
    // Demotion uses the UPPER bound of the current tier.
    // The gap between them is the hysteresis band.

    private const double ExcellentLossThreshold = 0.5;      // %
    private const double ExcellentJitterP99 = 20.0;          // ms
    private const double ExcellentJitterMax = 80.0;          // ms

    private const double GoodLossThreshold = 1.5;            // %
    private const double GoodJitterP99 = 40.0;               // ms
    private const double GoodJitterMax = 150.0;              // ms

    private const double DegradedLossThreshold = 3.0;        // %
    private const double DegradedJitterP99 = 80.0;           // ms
    private const double DegradedJitterMax = 250.0;          // ms

    // --- Prebuffer ranges per tier ---
    private const int ExcellentPrebufferMin = 40;
    private const int ExcellentPrebufferTarget = 60;
    private const int GoodPrebufferMin = 60;
    private const int GoodPrebufferTarget = 100;
    private const int DegradedPrebufferMin = 120;
    private const int DegradedPrebufferTarget = 180;
    private const int PoorPrebufferMin = 200;
    private const int PoorPrebufferTarget = 260;

    // --- Concealment windows per tier (in packets, each 10ms) ---
    private const int ExcellentConcealmentPackets = 5;
    private const int GoodConcealmentPackets = 10;
    private const int DegradedConcealmentPackets = 15;
    private const int PoorConcealmentPackets = 20; // matches current MaxConcealedGapPackets

    // --- Promotion/demotion cadence ---
    private const int WindowsForPromotion = 3;    // 3 consecutive good windows = 30 seconds
    private const int DemotionImmediate = 1;       // 1 bad window = instant demote
    private const int PrebufferStepUp = 20;        // ms to add when demoting
    private const int PrebufferStepDown = 10;      // ms to remove when promoting

    // --- Minimum packets for a valid evaluation ---
    private const long MinPacketsForEvaluation = 50;

    private LinkTier _currentTier = LinkTier.Good;
    private int _consecutiveGoodWindows;
    private int _consecutiveBadWindows;
    private int _recommendedPrebuffer = AudioPipeline.DefaultPrebufferMilliseconds;
    private int _recommendedConcealmentPackets = GoodConcealmentPackets;
    private bool _demotionSignaled;

    /// <summary>Current assessed link quality tier.</summary>
    public LinkTier CurrentTier => _currentTier;

    /// <summary>Recommended prebuffer in milliseconds, clamped to user bounds.</summary>
    public int RecommendedPrebuffer => _recommendedPrebuffer;

    /// <summary>Recommended concealment window in packets.</summary>
    public int RecommendedConcealmentPackets => _recommendedConcealmentPackets;

    /// <summary>True if the phone should be signaled to switch Wi-Fi lock mode.</summary>
    public bool ShouldSignalDemotion => _demotionSignaled;

    /// <summary>
    /// Evaluates the latest window stats and updates the tier and recommendations.
    /// Called once per 10-second analytics window.
    /// </summary>
    /// <param name="stats">The latest 10-second window stats.</param>
    /// <param name="userPrebufferMin">Lower bound from user's slider.</param>
    /// <param name="userPrebufferMax">Upper bound from user's slider.</param>
    /// <returns>The quality assessment for this window.</returns>
    public LinkAssessment Evaluate(WindowStats stats, int userPrebufferMin, int userPrebufferMax)
    {
        // Not enough data to assess — hold current settings.
        if (stats.PacketsReceived < MinPacketsForEvaluation)
        {
            return new LinkAssessment
            {
                Tier = _currentTier,
                Prebuffer = _recommendedPrebuffer,
                ConcealmentPackets = _recommendedConcealmentPackets,
                Action = LinkAction.Hold,
                Reason = $"Insufficient data ({stats.PacketsReceived} packets)",
            };
        }

        var loss = stats.LossPercent;
        var p99 = stats.InterarrivalP99Ms;
        var max = stats.InterarrivalMaxMs;

        // Classify this window's health.
        var windowTier = ClassifyWindow(loss, p99, max);

        // Apply hysteresis: promotion requires sustained good, demotion is immediate.
        var previousTier = _currentTier;
        LinkAction action;
        string reason;

        if (windowTier > _currentTier)
        {
            // Window is worse than current tier.
            _consecutiveBadWindows++;
            _consecutiveGoodWindows = 0;

            if (_consecutiveBadWindows >= DemotionImmediate)
            {
                _currentTier = windowTier;
                action = LinkAction.Demote;
                reason = $"Demoted to {windowTier}: loss={loss:F1}% p99={p99:F0}ms max={max:F0}ms";
                _demotionSignaled = windowTier == LinkTier.Poor;
            }
            else
            {
                action = LinkAction.Hold;
                reason = $"Bad window ({_consecutiveBadWindows}/{DemotionImmediate}): loss={loss:F1}% p99={p99:F0}ms";
            }
        }
        else if (windowTier < _currentTier)
        {
            // Window is better than current tier.
            _consecutiveGoodWindows++;
            _consecutiveBadWindows = 0;
            _demotionSignaled = false;

            if (_consecutiveGoodWindows >= WindowsForPromotion)
            {
                _currentTier = windowTier;
                _consecutiveGoodWindows = 0;
                action = LinkAction.Promote;
                reason = $"Promoted to {windowTier}: loss={loss:F1}% p99={p99:F0}ms (sustained {WindowsForPromotion} windows)";
            }
            else
            {
                action = LinkAction.Hold;
                reason = $"Improving ({_consecutiveGoodWindows}/{WindowsForPromotion}): loss={loss:F1}% p99={p99:F0}ms";
            }
        }
        else
        {
            // Holding steady.
            _consecutiveBadWindows = 0;
            // Don't reset good counter — it accumulates toward promotion.
            action = LinkAction.Hold;
            reason = $"Steady at {_currentTier}: loss={loss:F1}% p99={p99:F0}ms";
        }

        // Compute recommended values for the current tier.
        var (targetPrebuffer, concealment) = TierDefaults(_currentTier);

        // Smooth toward target rather than jumping.
        if (targetPrebuffer > _recommendedPrebuffer)
        {
            // Demoting: jump immediately to at least the tier minimum.
            _recommendedPrebuffer = Math.Max(targetPrebuffer, _recommendedPrebuffer + PrebufferStepUp);
        }
        else if (targetPrebuffer < _recommendedPrebuffer)
        {
            // Promoting: step down gradually.
            _recommendedPrebuffer = Math.Max(targetPrebuffer, _recommendedPrebuffer - PrebufferStepDown);
        }

        _recommendedConcealmentPackets = concealment;

        // Clamp to user bounds.
        var clampedPrebuffer = Math.Clamp(_recommendedPrebuffer, userPrebufferMin, userPrebufferMax);

        return new LinkAssessment
        {
            Tier = _currentTier,
            Prebuffer = clampedPrebuffer,
            ConcealmentPackets = _recommendedConcealmentPackets,
            Action = action,
            Reason = reason,
        };
    }

    /// <summary>
    /// Classifies a single window's health into a tier. Uses the LOWER bound of each
    /// tier as the entry condition — if any metric exceeds the threshold, the window
    /// is classified at the worse tier.
    /// </summary>
    private static LinkTier ClassifyWindow(double loss, double p99, double max)
    {
        // Excellent: everything is tight.
        if (loss < ExcellentLossThreshold && p99 <= ExcellentJitterP99 && max <= ExcellentJitterMax)
            return LinkTier.Excellent;

        // Good: tolerable for voice chat.
        if (loss < GoodLossThreshold && p99 <= GoodJitterP99 && max <= GoodJitterMax)
            return LinkTier.Good;

        // Degraded: audible artifacts likely.
        if (loss < DegradedLossThreshold && p99 <= DegradedJitterP99 && max <= DegradedJitterMax)
            return LinkTier.Degraded;

        // Poor: significant impairment.
        return LinkTier.Poor;
    }

    private static (int prebuffer, int concealmentPackets) TierDefaults(LinkTier tier) => tier switch
    {
        LinkTier.Excellent => (ExcellentPrebufferTarget, ExcellentConcealmentPackets),
        LinkTier.Good => (GoodPrebufferTarget, GoodConcealmentPackets),
        LinkTier.Degraded => (DegradedPrebufferTarget, DegradedConcealmentPackets),
        LinkTier.Poor => (PoorPrebufferTarget, PoorConcealmentPackets),
        _ => (GoodPrebufferTarget, GoodConcealmentPackets),
    };

    public void Reset()
    {
        _currentTier = LinkTier.Good;
        _consecutiveGoodWindows = 0;
        _consecutiveBadWindows = 0;
        _recommendedPrebuffer = AudioPipeline.DefaultPrebufferMilliseconds;
        _recommendedConcealmentPackets = GoodConcealmentPackets;
        _demotionSignaled = false;
    }
}

/// <summary>What the policy wants to do this window.</summary>
public enum LinkAction
{
    /// <summary>Current settings are appropriate. No change.</summary>
    Hold,

    /// <summary>Connection improved. Reduce prebuffer for lower latency.</summary>
    Promote,

    /// <summary>Connection degraded. Increase prebuffer for robustness.</summary>
    Demote,
}

/// <summary>
/// The policy's recommendation for this window. The engine applies the prebuffer
/// if it falls within the user's slider bounds; the concealment is applied directly;
/// the action and reason are surfaced in diagnostics.
/// </summary>
public sealed record LinkAssessment
{
    public LinkQualityPolicy.LinkTier Tier { get; init; }
    public int Prebuffer { get; init; }
    public int ConcealmentPackets { get; init; }
    public LinkAction Action { get; init; }
    public string Reason { get; init; } = "";
}
