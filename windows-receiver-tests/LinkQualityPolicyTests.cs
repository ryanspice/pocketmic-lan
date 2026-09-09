using PocketMicReceiver;
using Xunit;
using static PocketMicReceiver.LinkQualityPolicy;

namespace PocketMicReceiver.Tests;

public class LinkQualityPolicyTests
{
    private static WindowStats MakeStats(
        double lossPercent = 0,
        double p99 = 15,
        double max = 40,
        long packetsReceived = 200) => new(
        WindowKind: "test",
        WindowStart: DateTimeOffset.MinValue,
        WindowEnd: DateTimeOffset.MinValue,
        PacketsReceived: packetsReceived,
        PacketsLost: 0,
        Duplicates: 0,
        Reordered: 0,
        Rejected: 0,
        Trimmed: 0,
        LossPercent: lossPercent,
        InterarrivalP50Ms: 10,
        InterarrivalP95Ms: p99 * 0.8,
        InterarrivalP99Ms: p99,
        InterarrivalMaxMs: max,
        InterarrivalStdevMs: 5,
        BufferDepthAvgMs: 80,
        BufferDepthMaxMs: 120,
        LevelDbfsAvg: -30,
        LevelDbfsMin: -60,
        LevelDbfsMax: -10,
        LevelSamples: 200,
        NoiseGateActivePercent: 0,
        DroppedSamples: 0);

    [Fact]
    public void DefaultsToGoodTier()
    {
        var policy = new LinkQualityPolicy();
        Assert.Equal(LinkTier.Good, policy.CurrentTier);
    }

    [Fact]
    public void InsufficientDataHoldsCurrentSettings()
    {
        var policy = new LinkQualityPolicy();
        var stats = MakeStats(packetsReceived: 10);
        var result = policy.Evaluate(stats, 40, 300);
        Assert.Equal(LinkAction.Hold, result.Action);
        Assert.Contains("Insufficient data", result.Reason);
    }

    [Fact]
    public void ExcellentConditionsClassifyAsExcellent()
    {
        var policy = new LinkQualityPolicy();
        // Three consecutive excellent windows for promotion.
        for (var i = 0; i < 3; i++)
        {
            var stats = MakeStats(lossPercent: 0.1, p99: 15, max: 50);
            policy.Evaluate(stats, 40, 300);
        }
        Assert.Equal(LinkTier.Excellent, policy.CurrentTier);
    }

    [Fact]
    public void HighLossDemotesImmediately()
    {
        var policy = new LinkQualityPolicy();
        // Start at Good (default). One poor window should demote.
        var stats = MakeStats(lossPercent: 5.0, p99: 100, max: 300);
        var result = policy.Evaluate(stats, 40, 300);
        Assert.Equal(LinkAction.Demote, result.Action);
        Assert.Equal(LinkTier.Poor, policy.CurrentTier);
    }

    [Fact]
    public void PromotionRequiresSustainedGoodWindows()
    {
        var policy = new LinkQualityPolicy();
        // First two excellent windows — not enough for promotion.
        var stats = MakeStats(lossPercent: 0.1, p99: 15, max: 50);
        policy.Evaluate(stats, 40, 300);
        Assert.Equal(LinkTier.Good, policy.CurrentTier); // still Good

        policy.Evaluate(stats, 40, 300);
        Assert.Equal(LinkTier.Good, policy.CurrentTier); // still Good

        // Third window — now promotes.
        policy.Evaluate(stats, 40, 300);
        Assert.Equal(LinkTier.Excellent, policy.CurrentTier);
    }

    [Fact]
    public void DemotionResetsPromotionCounter()
    {
        var policy = new LinkQualityPolicy();
        var goodStats = MakeStats(lossPercent: 0.1, p99: 15, max: 50);

        // Two good windows toward promotion.
        policy.Evaluate(goodStats, 40, 300);
        policy.Evaluate(goodStats, 40, 300);

        // One bad window — resets the good counter and demotes.
        var badStats = MakeStats(lossPercent: 4.0, p99: 90, max: 300);
        policy.Evaluate(badStats, 40, 300);
        Assert.Equal(LinkTier.Poor, policy.CurrentTier);

        // Two more good windows — counter starts from zero, still at Poor.
        policy.Evaluate(goodStats, 40, 300);
        policy.Evaluate(goodStats, 40, 300);
        Assert.Equal(LinkTier.Poor, policy.CurrentTier); // not yet promoted

        // Third good window — promotes directly to Excellent (the window's actual tier).
        // The policy doesn't step through intermediate tiers; it jumps to the window
        // classification after the hysteresis period.
        policy.Evaluate(goodStats, 40, 300);
        Assert.Equal(LinkTier.Excellent, policy.CurrentTier);
    }

    [Fact]
    public void PrebufferClampedToUserBounds()
    {
        var policy = new LinkQualityPolicy();
        // Excellent tier wants 60ms target, but user min is 80ms.
        var stats = MakeStats(lossPercent: 0.1, p99: 15, max: 50);
        for (var i = 0; i < 3; i++)
            policy.Evaluate(stats, 80, 300);

        var result = policy.Evaluate(stats, 80, 300);
        Assert.True(result.Prebuffer >= 80, $"Prebuffer {result.Prebuffer} should be >= user min 80");
    }

    [Fact]
    public void PrebufferDoesNotExceedUserMax()
    {
        var policy = new LinkQualityPolicy();
        // Poor tier wants 260ms target, but user max is 200ms.
        var stats = MakeStats(lossPercent: 5.0, p99: 100, max: 300);
        var result = policy.Evaluate(stats, 40, 200);
        Assert.True(result.Prebuffer <= 200, $"Prebuffer {result.Prebuffer} should be <= user max 200");
    }

    [Fact]
    public void ConcealmentPacketsMatchTier()
    {
        var policy = new LinkQualityPolicy();

        // Excellent → 5 packets
        var excellentStats = MakeStats(lossPercent: 0.1, p99: 15, max: 50);
        for (var i = 0; i < 3; i++)
            policy.Evaluate(excellentStats, 40, 300);
        Assert.Equal(5, policy.RecommendedConcealmentPackets);

        // Demote to Poor → 20 packets
        var poorStats = MakeStats(lossPercent: 5.0, p99: 100, max: 300);
        policy.Evaluate(poorStats, 40, 300);
        Assert.Equal(20, policy.RecommendedConcealmentPackets);
    }

    [Fact]
    public void SignalDemotionOnlyAtPoorTier()
    {
        var policy = new LinkQualityPolicy();

        // Degraded — no signal.
        var degradedStats = MakeStats(lossPercent: 2.5, p99: 60, max: 200);
        policy.Evaluate(degradedStats, 40, 300);
        Assert.False(policy.ShouldSignalDemotion);

        // Poor — signal.
        var poorStats = MakeStats(lossPercent: 5.0, p99: 100, max: 300);
        policy.Evaluate(poorStats, 40, 300);
        Assert.True(policy.ShouldSignalDemotion);
    }

    [Fact]
    public void ResetReturnsToDefaults()
    {
        var policy = new LinkQualityPolicy();
        var poorStats = MakeStats(lossPercent: 5.0, p99: 100, max: 300);
        policy.Evaluate(poorStats, 40, 300);
        Assert.Equal(LinkTier.Poor, policy.CurrentTier);

        policy.Reset();
        Assert.Equal(LinkTier.Good, policy.CurrentTier);
        Assert.False(policy.ShouldSignalDemotion);
    }

    [Fact]
    public void JitterP99AloneCanDegrade()
    {
        var policy = new LinkQualityPolicy();
        // Low loss but high p99 jitter.
        var stats = MakeStats(lossPercent: 0.1, p99: 50, max: 80);
        var result = policy.Evaluate(stats, 40, 300);
        // p99=50 exceeds Good threshold (40ms) → should be Degraded or worse.
        Assert.True(result.Tier >= LinkTier.Degraded,
            $"Expected Degraded or worse for p99=50ms, got {result.Tier}");
    }

    [Fact]
    public void MaxJitterAloneCanDegrade()
    {
        var policy = new LinkQualityPolicy();
        // Good p99 but extreme max spike.
        var stats = MakeStats(lossPercent: 0.1, p99: 30, max: 200);
        var result = policy.Evaluate(stats, 40, 300);
        // max=200 exceeds Good threshold (150ms) → should be Degraded or worse.
        Assert.True(result.Tier >= LinkTier.Degraded,
            $"Expected Degraded or worse for max=200ms, got {result.Tier}");
    }
}
