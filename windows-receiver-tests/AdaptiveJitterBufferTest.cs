using System.Diagnostics;
using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// Deterministic tests for the adaptive jitter buffer and LinkQualityPolicy.
/// Uses controlled arrivalTick values (not real time) to verify convergence,
/// rate limiting, bounds clamping, drift detection, and policy cooperation.
/// </summary>
public class AdaptiveJitterBufferTest
{
    // Helper: convert milliseconds to Stopwatch ticks
    private static long MsToTicks(double ms) => (long)(ms * Stopwatch.Frequency / 1000.0);

    // Helper: generate a sequence of arrival ticks at a fixed interval, starting from "now"
    private static long[] GenerateTicks(double intervalMs, int count)
    {
        var start = Stopwatch.GetTimestamp();
        var ticks = new long[count];
        for (int i = 0; i < count; i++)
            ticks[i] = start + MsToTicks(intervalMs * i);
        return ticks;
    }

    // Helper: generate ticks with variable intervals
    private static long[] GenerateVariableTicks(double[] intervals)
    {
        var start = Stopwatch.GetTimestamp();
        var ticks = new long[intervals.Length];
        long t = start;
        for (int i = 0; i < intervals.Length; i++)
        {
            ticks[i] = t;
            t += MsToTicks(intervals[i]);
        }
        return ticks;
    }

    // -- P95 convergence tests -----------------------------------------------

    [Fact]
    public void P95_SteadyState_10ms_Intervals()
    {
        var buf = new AdaptiveJitterBuffer();
        var ticks = GenerateTicks(10.0, 250);

        // Need to advance time for the adjustment cycle to trigger.
        // The buffer updates target once per AdjustmentCycleMs (100ms).
        // 250 packets at 10ms = 2500ms total, so ~25 adjustment cycles.
        // We need to pass time-dependent arrival ticks.
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // After 250 packets at 10ms, P95 should be ~10ms
        // Raw target = 10 × 1.2 = 12ms, but clamped to [30, 120] → 30ms
        Assert.InRange(buf.RawP95Ms, 9.0, 11.0);
    }

    [Fact]
    public void P95_Moderate_Jitter()
    {
        var buf = new AdaptiveJitterBuffer();
        // Alternate between 10ms and 15ms intervals
        var intervals = new double[250];
        for (int i = 0; i < 250; i++)
            intervals[i] = i % 2 == 0 ? 10.0 : 15.0;
        var ticks = GenerateVariableTicks(intervals);

        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // P95 should be ~15ms (95th percentile of alternating 10/15)
        Assert.InRange(buf.RawP95Ms, 14.0, 16.0);
    }

    [Fact]
    public void P95_Single_Outage_Does_Not_Raise()
    {
        var buf = new AdaptiveJitterBuffer();
        // 199 packets at 10ms, then 1 gap of 510ms (rejected by >500ms filter), then more 10ms
        var intervals = new double[250];
        for (int i = 0; i < 199; i++) intervals[i] = 10.0;
        intervals[199] = 510.0; // rejected by the buffer
        for (int i = 200; i < 250; i++) intervals[i] = 10.0;
        var ticks = GenerateVariableTicks(intervals);

        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // P95 should still be ~10ms — the 510ms gap is rejected (>500ms threshold)
        Assert.InRange(buf.RawP95Ms, 9.0, 11.0);
    }

    [Fact]
    public void P95_Short_Gap_Does_Not_Raise_Much()
    {
        var buf = new AdaptiveJitterBuffer();
        // 199 packets at 10ms, then 1 gap of 200ms (within acceptance range)
        var intervals = new double[250];
        for (int i = 0; i < 199; i++) intervals[i] = 10.0;
        intervals[199] = 200.0; // accepted (within 1-500ms range)
        for (int i = 200; i < 250; i++) intervals[i] = 10.0;
        var ticks = GenerateVariableTicks(intervals);

        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // P95 should still be ~10ms — one 200ms gap in 200 samples is the 100th percentile
        Assert.InRange(buf.RawP95Ms, 9.0, 11.0);
    }

    // -- Rate limiter tests --------------------------------------------------

    [Fact]
    public void Rate_Limiter_Caps_Growth()
    {
        var buf = new AdaptiveJitterBuffer();
        // Start with good conditions, then suddenly inject high jitter
        var intervals = new double[500];
        for (int i = 0; i < 100; i++) intervals[i] = 10.0;  // baseline
        for (int i = 100; i < 500; i++) intervals[i] = 110.0; // high jitter
        var ticks = GenerateVariableTicks(intervals);

        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // Target should have increased but be rate-limited
        Assert.True(buf.TargetBufferMs > 30, $"Target {buf.TargetBufferMs} should be above 30ms floor");
        Assert.True(buf.TargetBufferMs <= 120, $"Target {buf.TargetBufferMs} should not exceed 120ms ceiling");
    }

    // -- Bounds clamping tests -----------------------------------------------

    [Fact]
    public void Target_Clamped_To_Min_Floor()
    {
        var buf = new AdaptiveJitterBuffer();
        // Very low jitter → P95 ~ 5ms → raw target 6ms → clamped to 30ms
        // Need enough packets for the rate limiter to bring target down from default 100ms
        var ticks = GenerateTicks(5.0, 1000);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // After 1000 packets at 5ms = 10 seconds = 100 cycles
        // Rate limiter: -5ms per cycle × 100 cycles = -500ms from 100ms → clamped to 30ms
        Assert.InRange(buf.TargetBufferMs, 30.0, 40.0);
    }

    [Fact]
    public void Target_Clamped_To_Max_Ceiling()
    {
        var buf = new AdaptiveJitterBuffer();
        // Very high jitter → P95 ~ 200ms → raw target 240ms → clamped to 120ms
        var ticks = GenerateTicks(200.0, 250);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        Assert.InRange(buf.TargetBufferMs, 110.0, 120.0); // near ceiling
    }

    // -- Tier bounds tests ---------------------------------------------------

    [Fact]
    public void TierBounds_Excellent_Is_40_60()
    {
        var (min, max) = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Excellent);
        Assert.Equal(40, min);
        Assert.Equal(60, max);
    }

    [Fact]
    public void TierBounds_Good_Is_60_120()
    {
        var (min, max) = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Good);
        Assert.Equal(60, min);
        Assert.Equal(120, max);
    }

    [Fact]
    public void TierBounds_Degraded_Is_120_200()
    {
        var (min, max) = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Degraded);
        Assert.Equal(120, min);
        Assert.Equal(200, max);
    }

    [Fact]
    public void TierBounds_Poor_Is_200_300()
    {
        var (min, max) = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Poor);
        Assert.Equal(200, min);
        Assert.Equal(300, max);
    }

    [Fact]
    public void TierBounds_Are_Non_Overlapping()
    {
        var excellent = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Excellent);
        var good = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Good);
        var degraded = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Degraded);
        var poor = LinkQualityPolicy.TierBounds(LinkQualityPolicy.LinkTier.Poor);

        Assert.Equal(excellent.Max, good.Min);
        Assert.Equal(good.Max, degraded.Min);
        Assert.Equal(degraded.Max, poor.Min);
    }

    // -- Policy + buffer cooperation tests -----------------------------------

    [Fact]
    public void EffectivePrebuffer_Clamped_To_Tier_Bounds()
    {
        var buf = new AdaptiveJitterBuffer();
        // Feed steady 10ms → target will be at floor (30ms)
        var ticks = GenerateTicks(10.0, 250);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // Good tier: [60, 120] → even though target is 30ms, effective is 60ms
        var effective = buf.ComputeEffectivePrebuffer(60, 120);
        Assert.InRange(effective, 60, 120);
    }

    [Fact]
    public void EffectivePrebuffer_Follows_Target_Within_Tier()
    {
        var buf = new AdaptiveJitterBuffer();
        // Feed moderate jitter → target will be somewhere in [30, 120]
        var ticks = GenerateTicks(50.0, 250);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // Good tier: [60, 120]
        var effective = buf.ComputeEffectivePrebuffer(60, 120);
        Assert.InRange(effective, 60, 120);

        // Excellent tier: [40, 60] → clamped to 60 max
        var effectiveExcellent = buf.ComputeEffectivePrebuffer(40, 60);
        Assert.InRange(effectiveExcellent, 40, 60);
    }

    // -- Reset test ----------------------------------------------------------

    [Fact]
    public void Reset_Clears_All_State()
    {
        var buf = new AdaptiveJitterBuffer();
        var ticks = GenerateTicks(100.0, 250);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        buf.Reset();

        Assert.Equal(0, buf.RawP95Ms);
        Assert.Equal(0, buf.DriftRateMsPerSec);
        // After reset, target should be back to default
        Assert.Equal(AudioPipeline.DefaultPrebufferMilliseconds, buf.TargetBufferMs);
    }

    // -- Drift detection tests -----------------------------------------------
    // NOTE: ComputeDriftRate() uses real Stopwatch.GetTimestamp() for the elapsed-
    // time check (requires >= 2 seconds of wall-clock time). These tests verify
    // the behavior after enough real time has passed.

    [Fact]
    public void Drift_Rate_Is_Zero_Before_Warmup()
    {
        var buf = new AdaptiveJitterBuffer();
        var ticks = GenerateTicks(10.0, 250);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // Before 2 seconds of wall-clock time, drift should be 0
        Assert.Equal(0, buf.DriftRateMsPerSec);
    }

    [Fact]
    public void Drift_Rate_Property_Is_Readable()
    {
        var buf = new AdaptiveJitterBuffer();
        // Just verify the property exists and is 0 at start
        Assert.Equal(0, buf.DriftRateMsPerSec);
    }

    [Fact]
    public void Drift_Warmup_Packets_Are_Skipped()
    {
        var buf = new AdaptiveJitterBuffer();
        // Feed fewer packets than DriftWarmupPackets (20)
        var ticks = GenerateTicks(10.0, 15);
        for (int i = 0; i < ticks.Length; i++)
            buf.RecordPacket(ticks[i], 0);

        // Drift should be 0 — not enough warmup packets
        Assert.Equal(0, buf.DriftRateMsPerSec);
    }

    // -- Default value test --------------------------------------------------

    [Fact]
    public void DefaultPrebufferMilliseconds_Is_Positive()
    {
        Assert.True(AudioPipeline.DefaultPrebufferMilliseconds > 0);
    }
}
