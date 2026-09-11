using System.Diagnostics;

namespace PocketMicReceiver;

/// <summary>
/// Percentile-based adaptive jitter buffer that fine-tunes the prebuffer target
/// within the tier bounds established by <see cref="LinkQualityPolicy"/>.
///
/// The policy classifies the link into quality tiers every 10 seconds; this buffer
/// continuously tracks per-packet interarrival jitter and adjusts the target prebuffer
/// depth so the receiver stays as close to the real-time edge as the current network
/// conditions allow.
///
/// Three cooperating mechanisms:
/// 1. <b>Percentile delay estimator</b>: a rolling window of interarrival times,
///    P95 × 1.2, clamped to [30, 120] ms.  The 1.2× headroom absorbs the occasional
///    spike that falls between P95 and P99 without over-provisioning for the common case.
/// 2. <b>Rate limiter</b>: target changes are capped at ±5 ms per 100 ms wall-clock
///    window, preventing audible step changes in latency.
/// 3. <b>Clock drift compensation</b>: a linear regression of buffer depth over a 5 s
///    window detects persistent growth (sender clock faster) or shrinkage (sender clock
///    slower).  The target is nudged to counteract the trend, preventing the buffer from
///    slowly filling or starving.
///
/// The <see cref="LinkQualityPolicy"/> sets the tier; this buffer fine-tunes within it.
/// The engine calls <see cref="RecordPacket"/> once per delivered packet and reads
/// <see cref="ComputeEffectivePrebuffer"/> to obtain the clamped target.
/// </summary>
public sealed class AdaptiveJitterBuffer
{
    // --- Tuning constants ---

    /// <summary>Number of interarrival samples in the rolling window. At 100 pkt/s
    /// this covers the last ~2 seconds of arrivals.</summary>
    private const int InterarrivalWindowSize = 200;

    /// <summary>Target buffer = P95 × this multiplier. 1.2× gives one-fifth headroom
    /// beyond the 95th percentile without inflating latency for the common case.</summary>
    private const double PercentileMultiplier = 1.2;

    /// <summary>Absolute floor: no matter how good the link, maintain at least 30 ms
    /// of buffering to absorb micro-jitter.</summary>
    private const double MinTargetMs = 30;

    /// <summary>Absolute ceiling: beyond 120 ms the round-trip delay becomes noticeable
    /// in a voice conversation.</summary>
    private const double MaxTargetMs = 120;

    /// <summary>Maximum change per adjustment cycle. At 100 ms per cycle this is ±50 ms/s,
    /// meaning a 50 ms adjustment takes a full second — fast enough to track real
    /// condition changes, slow enough to be completely inaudible.</summary>
    private const double MaxAdjustmentPerCycleMs = 5.0;

    /// <summary>Adjustment cycle length in milliseconds. The target is recomputed at most
    /// once per cycle, not per packet, so the percentile sort runs ≤10 times/second.</summary>
    private const int AdjustmentCycleMs = 100;

    /// <summary>Samples in the drift regression window. At one sample per 100 ms cycle,
    /// 50 samples = 5 seconds of buffer-depth history — long enough to distinguish a real
    /// trend from transient bursts.</summary>
    private const int DriftWindowSize = 50;

    /// <summary>Minimum drift rate (ms/s) to trigger compensation. Below this the
    /// adjustment would chase noise rather than a real clock-frequency offset.</summary>
    private const double DriftThresholdMsPerSec = 1.5;

    /// <summary>Target adjustment when drift is detected. A 5 ms nudge per 100 ms cycle
    /// counteracts drift of ~50 ms/s, which is far beyond normal clock mismatch
    /// (typically &lt;0.1 ms/s for晶振-grade oscillators).</summary>
    private const double DriftCompensationMs = 5.0;

    /// <summary>Minimum interarrival samples before the buffer depth drift window starts
    /// collecting, to avoid garbage data during the initial ramp-up.</summary>
    private const int DriftWarmupPackets = 20;

    // --- Interarrival ring buffer ---
    private readonly double[] _interarrivalMs = new double[InterarrivalWindowSize];
    private int _interarrivalCount;
    private long _lastArrivalTick;

    // --- Rate-limited target ---
    private double _currentTargetMs;
    private long _lastAdjustmentTick;

    // --- Drift detection ---
    private readonly double[] _bufferDepths = new double[DriftWindowSize];
    private int _depthCount;
    private long _driftWindowStartTick;

    // --- Pre-allocated sort buffer to avoid per-call allocation on the hot path ---
    private readonly double[] _sortBuffer = new double[InterarrivalWindowSize];

    // --- Public readouts (consumed by diagnostics / session log / engine) ---

    /// <summary>Raw P95 interarrival time before the multiplier and clamping.
    /// Exposed so the session recorder can log the measured jitter alongside the
    /// adjusted buffer depth.</summary>
    public double RawP95Ms { get; private set; }

    /// <summary>Measured drift rate in ms/s. Positive means the buffer is growing
    /// (sender clock faster than receiver playback); negative means it is shrinking
    /// (sender clock slower).</summary>
    public double DriftRateMsPerSec { get; private set; }

    /// <summary>The rate-limited target buffer depth in milliseconds. This is the value
    /// the engine should pass to <see cref="ComputeEffectivePrebuffer"/> to obtain
    /// the tier-clamped result.</summary>
    public double TargetBufferMs => _currentTargetMs;

    public AdaptiveJitterBuffer()
    {
        _currentTargetMs = AudioPipeline.DefaultPrebufferMilliseconds;
        _lastAdjustmentTick = Stopwatch.GetTimestamp();
        _driftWindowStartTick = Stopwatch.GetTimestamp();
    }

    /// <summary>Resets all state. Called on session resync or at the start of a new run.</summary>
    public void Reset()
    {
        Array.Clear(_interarrivalMs);
        _interarrivalCount = 0;
        _lastArrivalTick = 0;

        _currentTargetMs = AudioPipeline.DefaultPrebufferMilliseconds;
        _lastAdjustmentTick = Stopwatch.GetTimestamp();

        Array.Clear(_bufferDepths);
        _depthCount = 0;
        _driftWindowStartTick = Stopwatch.GetTimestamp();

        RawP95Ms = 0;
        DriftRateMsPerSec = 0;
    }

    /// <summary>
    /// Records a delivered packet's arrival time and current buffer depth.
    /// Call once per authenticated, in-sequence packet in the receive loop, ideally
    /// right after <c>analytics.RecordDelivered</c> so the interarrival measurement
    /// is as close to the true network arrival time as possible.
    /// </summary>
    /// <param name="arrivalTick">High-resolution monotonic timestamp of this packet's
    /// arrival (use <see cref="Stopwatch.GetTimestamp()"/>)</param>
    /// <param name="bufferedMs">Current buffer depth in milliseconds at the time of
    /// recording, typically <c>buffer.BufferedDuration.TotalMilliseconds</c>.</param>
    public void RecordPacket(long arrivalTick, double bufferedMs)
    {
        // --- Interarrival ---
        if (_lastArrivalTick != 0)
        {
            var deltaMs = (arrivalTick - _lastArrivalTick) * 1000.0 / Stopwatch.Frequency;

            // Reject absurd gaps (>500 ms) that indicate a pause/resume or a burst of
            // retransmissions rather than genuine jitter — including them would corrupt
            // the percentile estimate and inflate the target for no benefit.
            if (deltaMs is > 1.0 and < 500.0)
            {
                _interarrivalMs[_interarrivalCount % InterarrivalWindowSize] = deltaMs;
                _interarrivalCount++;
            }
        }
        _lastArrivalTick = arrivalTick;

        // --- Buffer depth for drift regression (skip warmup to avoid garbage) ---
        if (_interarrivalCount > DriftWarmupPackets)
        {
            _bufferDepths[_depthCount % DriftWindowSize] = bufferedMs;
            _depthCount++;
        }

        // --- Update target (rate-limited internally to once per AdjustmentCycleMs) ---
        UpdateTarget(arrivalTick);
    }

    /// <summary>
    /// Computes the effective prebuffer for the current tier bounds.  The adaptive
    /// target is clamped so it never violates the tier's min/max, ensuring the
    /// adaptive buffer only fine-tunes within the policy's range.
    /// </summary>
    /// <param name="tierPrebufferMin">Minimum prebuffer for the current tier,
    /// from <see cref="LinkQualityPolicy.TierBounds"/>.</param>
    /// <param name="tierPrebufferMax">Maximum prebuffer for the current tier.</param>
    /// <returns>The effective prebuffer in milliseconds.</returns>
    public int ComputeEffectivePrebuffer(int tierPrebufferMin, int tierPrebufferMax)
    {
        return Math.Clamp((int)Math.Round(_currentTargetMs), tierPrebufferMin, tierPrebufferMax);
    }

    // --- Private helpers ---

    private void UpdateTarget(long nowTick)
    {
        var elapsed = nowTick - _lastAdjustmentTick;
        var elapsedMs = elapsed * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs < AdjustmentCycleMs) return;

        // 1. Percentile-based target
        RawP95Ms = ComputePercentile(0.95);
        var rawTarget = RawP95Ms * PercentileMultiplier;

        // 2. Drift compensation
        DriftRateMsPerSec = ComputeDriftRate();
        var driftAdjust = 0.0;
        if (DriftRateMsPerSec > DriftThresholdMsPerSec)
        {
            // Buffer growing persistently: sender clock is faster → shrink target
            // so the excess is trimmed rather than accumulating as latency.
            driftAdjust = -DriftCompensationMs;
        }
        else if (DriftRateMsPerSec < -DriftThresholdMsPerSec)
        {
            // Buffer shrinking persistently: sender clock is slower → grow target
            // to add headroom and prevent underruns.
            driftAdjust = DriftCompensationMs;
        }

        var desiredTarget = Math.Clamp(rawTarget + driftAdjust, MinTargetMs, MaxTargetMs);

        // 3. Rate limit: cap the change to ±MaxAdjustmentPerCycleMs per cycle.
        //    The elapsed-ms divisor normalises for cycles that ran slightly long.
        var maxDelta = MaxAdjustmentPerCycleMs * (elapsedMs / AdjustmentCycleMs);
        var delta = desiredTarget - _currentTargetMs;
        delta = Math.Clamp(delta, -maxDelta, maxDelta);

        _currentTargetMs = Math.Clamp(_currentTargetMs + delta, MinTargetMs, MaxTargetMs);
        _lastAdjustmentTick = nowTick;
    }

    /// <summary>
    /// Computes the given percentile of the interarrival window using the
    /// pre-allocated sort buffer.  O(n log n) but runs at most 10 times per second
    /// on a window of ≤200 elements — negligible cost.
    /// </summary>
    private double ComputePercentile(double percentile)
    {
        var count = Math.Min(_interarrivalCount, InterarrivalWindowSize);
        if (count == 0) return 10.0; // sensible default: 10 ms interarrival ≈ 100 pkt/s

        Array.Copy(_interarrivalMs, _sortBuffer, count);
        Array.Sort(_sortBuffer, 0, count);

        // Ceiling-index approach: P95 of 200 samples = index 190 (0-based).
        var index = (int)Math.Ceiling(percentile * count) - 1;
        return _sortBuffer[Math.Clamp(index, 0, count - 1)];
    }

    /// <summary>
    /// Linear regression of buffer depth over the drift window.
    /// Returns the slope in ms/s: positive means buffer is growing, negative means
    /// shrinking.  Uses ordinary least squares on the most recent
    /// <see cref="DriftWindowSize"/> samples.
    /// </summary>
    private double ComputeDriftRate()
    {
        var count = Math.Min(_depthCount, DriftWindowSize);
        if (count < 10) return 0;

        // Require at least 2 seconds of data for a meaningful trend — a shorter
        // window would be dominated by normal jitter fluctuation.
        var elapsedMs = (Stopwatch.GetTimestamp() - _driftWindowStartTick) * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs < 2000) return 0;

        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (var i = 0; i < count; i++)
        {
            var x = (double)i;
            var y = _bufferDepths[i];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXX += x * x;
        }

        var n = (double)count;
        var denominator = (n * sumXX) - (sumX * sumX);
        if (Math.Abs(denominator) < 1e-10) return 0;

        var slopePerSample = ((n * sumXY) - (sumX * sumY)) / denominator;

        // Each sample is recorded roughly once per AdjustmentCycleMs (100 ms).
        // Convert slope from ms/sample to ms/s.
        var samplesPerSecond = 1000.0 / AdjustmentCycleMs;
        return slopePerSample * samplesPerSecond;
    }
}
