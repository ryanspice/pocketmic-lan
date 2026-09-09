namespace PocketMicReceiver;

/// <summary>
/// Online mean/variance/min/max via Welford's algorithm, so a running window never has to keep
/// the raw samples around just to answer "what was the average buffer depth". One update is a
/// handful of floating point ops; memory use is fixed regardless of how long the window runs.
/// </summary>
internal struct RunningStat
{
    private long _count;
    private double _mean;
    private double _m2;
    private double _min;
    private double _max;
    private double _last;

    public static RunningStat Create() => new() { _min = double.PositiveInfinity, _max = double.NegativeInfinity };

    public void Add(double value)
    {
        _count++;
        var delta = value - _mean;
        _mean += delta / _count;
        _m2 += delta * (value - _mean);
        if (value < _min) _min = value;
        if (value > _max) _max = value;
        _last = value;
    }

    public readonly long Count => _count;
    public readonly double Mean => _count > 0 ? _mean : 0;
    public readonly double Min => _count > 0 ? _min : 0;
    public readonly double Max => _count > 0 ? _max : 0;
    public readonly double Last => _last;
    public readonly double Variance => _count > 1 ? _m2 / (_count - 1) : 0;
    public readonly double Stdev => Math.Sqrt(Variance);

    public void Reset() => this = Create();
}

/// <summary>
/// Millisecond-resolution histogram used only for interarrival percentiles. A fixed bucket
/// array bounds memory use regardless of session length — the alternative, keeping every raw
/// interarrival sample so an exact percentile can be computed later, grows without limit over a
/// multi-hour soak. 1 ms buckets up to <see cref="CapacityMs"/> comfortably cover the worst
/// measured tail on this network (234-282 ms against a 10 ms ideal) with headroom to spare;
/// <see cref="Max"/> itself is tracked exactly and separately, so an extreme outlier is never
/// silently clipped even though the percentile buckets saturate.
/// </summary>
internal sealed class MillisecondHistogram
{
    public const int CapacityMs = 4_000;

    private readonly long[] _buckets = new long[CapacityMs];
    private RunningStat _stat = RunningStat.Create();

    public long Count => _stat.Count;
    public double Max => _stat.Max;
    public double Stdev => _stat.Stdev;

    public void Add(double milliseconds)
    {
        _stat.Add(milliseconds);
        var index = (int)Math.Clamp(milliseconds, 0, CapacityMs - 1);
        _buckets[index]++;
    }

    /// <summary>Nearest-rank percentile at 1 ms resolution, which is all the jitter numbers here need.</summary>
    public double Percentile(double p)
    {
        var count = _stat.Count;
        if (count == 0) return 0;

        var rank = (long)Math.Ceiling(p / 100.0 * count);
        rank = Math.Clamp(rank, 1, count);

        long cumulative = 0;
        for (var index = 0; index < _buckets.Length; index++)
        {
            cumulative += _buckets[index];
            if (cumulative >= rank) return index;
        }

        return CapacityMs - 1;
    }

    public void Reset()
    {
        Array.Clear(_buckets);
        _stat.Reset();
    }
}

/// <summary>
/// Accumulates one rolling window's worth of packet samples. The same aggregator type backs the
/// 10 s tumbling window, the 1 min tumbling window, and the whole-session cumulative window —
/// the only difference is whether and how often <see cref="Reset"/> is called. Every sample that
/// passes through <see cref="AnalyticsCollector"/> is fed into all three in parallel; at 100
/// packets/s the extra bookkeeping is a handful of double-precision ops per packet on a
/// background thread, nowhere near the audio path.
///
/// Guarded by a lock because, unlike the ring buffer, more than one thread can legitimately want
/// a consistent snapshot (the background drain thread writing, a report or UI reader on demand).
/// Contention is negligible: at most one writer, ticking a few times a second.
/// </summary>
internal sealed class WindowAggregator
{
    private readonly object _lock = new();
    private DateTimeOffset _windowStart = DateTimeOffset.UtcNow;

    private long _received;
    private long _lost;
    private long _duplicates;
    private long _reordered;
    private long _rejected;
    private long _trimmed;
    private long _gateActiveSamples;
    private long _levelSamples;

    private readonly MillisecondHistogram _interarrival = new();
    private RunningStat _bufferDepth = RunningStat.Create();
    private RunningStat _level = RunningStat.Create();

    public void Add(in PacketSample sample, double? interarrivalMs)
    {
        lock (_lock)
        {
            switch (sample.Outcome)
            {
                // The gate is reported over delivered packets, not over packets whose level
                // could be measured. Digital silence carries no level at all, and it is exactly
                // when the gate is most likely to be closed — counting the gate only where a
                // level exists would quietly drop the most gated frames out of its own
                // denominator and understate it.
                case PacketOutcome.Delivered:
                    _received++;
                    if (sample.GateActive) _gateActiveSamples++;
                    break;

                case PacketOutcome.Duplicate: _duplicates++; break;
                case PacketOutcome.Reordered: _reordered++; break;
                case PacketOutcome.Lost: _lost += sample.Count; break;
                case PacketOutcome.Rejected: _rejected++; break;
                case PacketOutcome.Trimmed: _trimmed++; break;
            }

            if (interarrivalMs.HasValue) _interarrival.Add(interarrivalMs.Value);
            if (sample.BufferMs != PacketSample.NoBufferMs) _bufferDepth.Add(sample.BufferMs);

            // Only a measured level. Silence arrives as the NoLevel sentinel and is skipped, so
            // a muted phone leaves the level statistics empty rather than dragging the average
            // towards a saturated floor that was never a real measurement.
            if (sample.LevelCentiDbfs != PacketSample.NoLevel)
            {
                _level.Add(sample.LevelCentiDbfs / 100.0);
                _levelSamples++;
            }
        }
    }

    public WindowStats Build(string windowKind, DateTimeOffset now, long droppedSamples)
    {
        lock (_lock)
        {
            var expected = _received + _lost;
            var lossPercent = expected > 0 ? 100.0 * _lost / expected : 0.0;
            var gatePercent = _received > 0 ? 100.0 * _gateActiveSamples / _received : 0.0;

            return new WindowStats(
                WindowKind: windowKind,
                WindowStart: _windowStart,
                WindowEnd: now,
                PacketsReceived: _received,
                PacketsLost: _lost,
                Duplicates: _duplicates,
                Reordered: _reordered,
                Rejected: _rejected,
                Trimmed: _trimmed,
                LossPercent: lossPercent,
                InterarrivalP50Ms: _interarrival.Percentile(50),
                InterarrivalP95Ms: _interarrival.Percentile(95),
                InterarrivalP99Ms: _interarrival.Percentile(99),
                InterarrivalMaxMs: _interarrival.Max,
                InterarrivalStdevMs: _interarrival.Stdev,
                BufferDepthAvgMs: _bufferDepth.Mean,
                BufferDepthMaxMs: _bufferDepth.Max,
                LevelDbfsAvg: _level.Mean,
                LevelDbfsMin: _level.Min,
                LevelDbfsMax: _level.Max,
                LevelSamples: _levelSamples,
                NoiseGateActivePercent: gatePercent,
                DroppedSamples: droppedSamples);
        }
    }

    public void Reset(DateTimeOffset now)
    {
        lock (_lock)
        {
            _windowStart = now;
            _received = 0;
            _lost = 0;
            _duplicates = 0;
            _reordered = 0;
            _rejected = 0;
            _trimmed = 0;
            _gateActiveSamples = 0;
            _levelSamples = 0;
            _interarrival.Reset();
            _bufferDepth.Reset();
            _level.Reset();
        }
    }
}
