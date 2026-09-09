using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// The diagnostics layer's whole value is that its numbers can be trusted, so the cases tested
/// here are the ones where it previously produced a confident wrong answer rather than no answer.
/// </summary>
public class AnalyticsTests
{
    /// <summary>
    /// Records into a collector and returns its whole-session window.
    ///
    /// The collector is disposed before the flush on purpose. Its sample queue is
    /// single-producer/single-consumer by design, the background thread is the consumer, and
    /// <see cref="AnalyticsCollector.FlushFinal"/> drains from the calling thread — so the
    /// background thread has to have stopped before the test thread drains, or the test would be
    /// exercising a violation of the queue's own contract.
    /// </summary>
    private static WindowStats BuildSession(Action<AnalyticsCollector> record)
    {
        var collector = new AnalyticsCollector();
        WindowStats? session = null;
        collector.WindowClosed += window =>
        {
            if (window.WindowKind == "session") session = window;
        };

        record(collector);
        collector.Dispose();
        collector.FlushFinal();

        Assert.NotNull(session);
        return session!;
    }

    /// <summary>
    /// The regression this exists for: <c>RecordDelivered(float.NegativeInfinity)</c> is a real
    /// number to <c>Math.Clamp</c>, so it saturated at the bottom of the short range and was then
    /// averaged in as though it were a measurement. A muted phone reported -327.67 dBFS.
    /// </summary>
    [Fact]
    public void DigitalSilenceIsExcludedFromLevelStatisticsRatherThanSaturatingThem()
    {
        var session = BuildSession(collector =>
        {
            for (var index = 0; index < 50; index++)
            {
                collector.RecordDelivered(60, float.NaN, gateActive: true);
            }
        });

        Assert.Equal(50, session.PacketsReceived);
        Assert.Equal(0, session.LevelSamples);
        Assert.Equal(0.0, session.LevelDbfsAvg);
        Assert.Equal(0.0, session.LevelDbfsMin);
        Assert.Equal(0.0, session.LevelDbfsMax);
    }

    [Fact]
    public void AMeasuredLevelIsAveragedAndTheSilenceAroundItIsNot()
    {
        var session = BuildSession(collector =>
        {
            collector.RecordDelivered(60, float.NaN, gateActive: true);
            collector.RecordDelivered(60, -20f, gateActive: false);
            collector.RecordDelivered(60, float.NaN, gateActive: true);
        });

        Assert.Equal(3, session.PacketsReceived);
        Assert.Equal(1, session.LevelSamples);
        Assert.Equal(-20.0, session.LevelDbfsAvg, 3);
        Assert.Equal(-20.0, session.LevelDbfsMin, 3);
        Assert.Equal(-20.0, session.LevelDbfsMax, 3);
    }

    /// <summary>
    /// Silence is exactly when the gate is most likely to be closed, so counting gate activity
    /// only over packets with a measurable level would drop the most gated frames out of the
    /// gate's own denominator.
    /// </summary>
    [Fact]
    public void TheNoiseGateIsReportedOverDeliveredPacketsNotOnlyOverMeasurableOnes()
    {
        var session = BuildSession(collector =>
        {
            collector.RecordDelivered(60, float.NaN, gateActive: true);
            collector.RecordDelivered(60, float.NaN, gateActive: true);
            collector.RecordDelivered(60, -18f, gateActive: false);
            collector.RecordDelivered(60, -18f, gateActive: false);
        });

        Assert.Equal(4, session.PacketsReceived);
        Assert.Equal(2, session.LevelSamples);
        Assert.Equal(50.0, session.NoiseGateActivePercent, 3);
    }

    [Fact]
    public void PacketsThatNeverArrivedAreNotCountedAgainstTheGate()
    {
        var session = BuildSession(collector =>
        {
            collector.RecordDelivered(60, -18f, gateActive: true);
            collector.RecordLost(9);
            collector.RecordRejected();
            collector.RecordTrimmed(200);
        });

        Assert.Equal(1, session.PacketsReceived);
        Assert.Equal(9, session.PacketsLost);
        Assert.Equal(100.0, session.NoiseGateActivePercent, 3);
    }

    [Fact]
    public void ConnectionAttemptsAndFirstAuthenticatedPacketAreRecorded()
    {
        var tracker = new ConnectionTracker(ReceiverFrontEnd.Classic, "0.1.2");
        Assert.Null(tracker.Snapshot().TimeToFirstPacketMs);

        tracker.MarkAttempt();
        tracker.MarkAuthenticatedPacket();
        tracker.MarkAuthenticatedPacket();

        var summary = tracker.Snapshot();
        Assert.Equal(1, summary.Attempts);
        Assert.NotNull(summary.TimeToFirstPacketMs);
        Assert.True(summary.TimeToFirstPacketMs >= 0);
        Assert.Equal(ReceiverFrontEnd.Classic, summary.FrontEnd);
        Assert.Equal("0.1.2", summary.BuildVersion);
    }

    /// <summary>
    /// The control tick calls <see cref="ConnectionTracker.MarkSilenceBegan"/> on every pass
    /// while audio is absent, so a silence that lasts ten seconds must still be one episode.
    /// </summary>
    [Fact]
    public void RepeatedSilenceReportsCollapseIntoOneEpisodeAndOneReconnect()
    {
        var tracker = new ConnectionTracker(ReceiverFrontEnd.Modern, "0.1.3");

        Assert.True(tracker.MarkSilenceBegan());
        Assert.False(tracker.MarkSilenceBegan());
        Assert.False(tracker.MarkSilenceBegan());

        var elapsed = tracker.MarkSilenceEnded();
        Assert.NotNull(elapsed);

        var summary = tracker.Snapshot();
        Assert.Equal(1, summary.SilenceEpisodes);
        Assert.Equal(1, summary.ReconnectCount);
        Assert.True(summary.TotalSilenceMs >= 0);
        Assert.True(summary.LongestSilenceMs >= 0);
    }

    [Fact]
    public void EndingASilenceThatNeverBeganIsNotAReconnect()
    {
        var tracker = new ConnectionTracker(ReceiverFrontEnd.Unknown, string.Empty);

        Assert.Null(tracker.MarkSilenceEnded());
        Assert.Equal(0, tracker.Snapshot().ReconnectCount);
        Assert.Equal(0, tracker.Snapshot().SilenceEpisodes);
    }

    /// <summary>
    /// A run that ends mid-outage has to report the outage. Discarding an episode because it was
    /// still open would hide the longest silences — the only ones anybody investigates.
    /// </summary>
    [Fact]
    public void AnOutageStillOpenWhenTheRunEndsIsIncludedInTheTotals()
    {
        var tracker = new ConnectionTracker(ReceiverFrontEnd.Classic, "0.1.2");
        tracker.MarkSilenceBegan();

        var summary = tracker.Snapshot();
        Assert.Equal(1, summary.SilenceEpisodes);
        Assert.Equal(0, summary.ReconnectCount);
        Assert.True(summary.TotalSilenceMs >= 0);
    }

    [Fact]
    public void DiscoveryFailuresAndVersionMismatchesAreCountedSeparately()
    {
        var tracker = new ConnectionTracker(ReceiverFrontEnd.Classic, "0.1.2");
        tracker.MarkDiscoveryFailure();
        tracker.MarkVersionMismatch();
        tracker.MarkVersionMismatch();

        var summary = tracker.Snapshot();
        Assert.Equal(1, summary.DiscoveryFailures);
        Assert.Equal(2, summary.VersionMismatches);
    }

    /// <summary>
    /// The session file is what gets attached to a bug report, often with no tool to render it,
    /// so the connection story has to be readable in the raw JSONL.
    /// </summary>
    [Fact]
    public void TheFinalFlushWritesAConnectionSummaryEvent()
    {
        var collector = new AnalyticsCollector(ReceiverFrontEnd.Modern, "0.1.3");
        var events = new List<SessionEventRecord>();
        collector.EventLogged += events.Add;

        collector.Connection.MarkAttempt();
        collector.Dispose();
        collector.FlushFinal();

        var summary = Assert.Single(events, evt => evt.Kind == "connection-summary");
        Assert.Contains("Modern 0.1.3", summary.Detail);
        Assert.Contains("attempts 1", summary.Detail);
        Assert.Contains("never", summary.Detail);
    }

    [Fact]
    public void TheBuildVersionComesFromTheAssemblyRatherThanAConstant()
    {
        var version = ReceiverBuild.VersionOf(typeof(AnalyticsCollector).Assembly);

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.DoesNotContain("+", version);
    }
}
