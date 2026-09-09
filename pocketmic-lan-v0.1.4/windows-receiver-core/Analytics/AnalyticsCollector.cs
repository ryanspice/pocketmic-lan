using System.Diagnostics;

namespace PocketMicReceiver;

/// <summary>
/// Rolling-window diagnostics for the receive loop: packet loss, duplicate/reorder counts,
/// interarrival percentiles, trim rate, buffer depth, input level, and noise-gate activity, at
/// 10 s, 1 min, and whole-session granularity.
///
/// <b>The one rule everything here is built around: the audio receive loop may only call
/// <see cref="RecordDelivered"/>, <see cref="RecordDuplicate"/>, <see cref="RecordReordered"/>,
/// <see cref="RecordLost"/>, <see cref="RecordRejected"/>, or <see cref="RecordTrimmed"/>, and
/// every one of those does nothing but timestamp a small struct and push it onto a lock-free
/// queue.</b> No allocation, no lock, no I/O, no percentile math — that all happens later, off a
/// dedicated background thread, on whatever cadence <see cref="DrainInterval"/> gives it. This
/// mirrors the discipline already in the receive loop itself (reused buffers, throttled UI
/// updates); analytics must never become the reason a packet is late.
///
/// <see cref="RecordDiscoveryEvent"/> and <see cref="RecordFault"/> are the one exception: they
/// are called from the control channel and fault-handling paths, which are already low
/// frequency and already allocate (string formatting, UI updates), so there is nothing to
/// protect there and they fire their event synchronously on the caller's thread.
/// </summary>
public sealed class AnalyticsCollector : IDisposable
{
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan TenSecondSpan = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OneMinuteSpan = TimeSpan.FromMinutes(1);

    private readonly PacketSampleQueue _queue;
    private readonly WindowAggregator _tenSecond = new();
    private readonly WindowAggregator _oneMinute = new();
    private readonly WindowAggregator _session = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _thread;

    private DateTimeOffset _tenSecondStart = DateTimeOffset.UtcNow;
    private DateTimeOffset _oneMinuteStart = DateTimeOffset.UtcNow;
    private long? _lastArrivalTicks;
    private bool _disposed;

    /// <summary>
    /// Fired from the background thread whenever a window closes: every 10 s for the "10s"
    /// window, every 1 min for the "1m" window and a fresh cumulative "session" snapshot
    /// alongside it. Never fired from the audio thread. A WinForms host must marshal to its UI
    /// thread itself (e.g. <c>Control.BeginInvoke</c>) exactly as it already does for receive-loop
    /// UI updates — this type deliberately has no UI-framework dependency to stay shareable with
    /// a WinUI 3 host.
    /// </summary>
    public event Action<WindowStats>? WindowClosed;

    /// <summary>Fired synchronously, on the caller's thread, from <see cref="RecordDiscoveryEvent"/> and <see cref="RecordFault"/>.</summary>
    public event Action<SessionEventRecord>? EventLogged;

    /// <summary>
    /// How many samples the audio thread has had to drop because the queue was full, i.e. the
    /// background thread fell behind. Should stay at zero; a nonzero value is itself a
    /// diagnostic signal worth surfacing, not a value the audio path should ever wait to avoid.
    /// </summary>
    public long DroppedSampleCount => _queue.DroppedCount;

    /// <summary>
    /// Connection health for this run: attempts, time to first authenticated packet, silence
    /// episodes and recoveries, discovery failures and version mismatches.
    ///
    /// Kept beside the packet analytics rather than in the engine because it answers the other
    /// half of the same question. The packet windows say how the audio behaved; this says
    /// whether there was a link at all, and both belong in the one session file.
    /// </summary>
    public ConnectionTracker Connection { get; }

    public AnalyticsCollector(
        ReceiverFrontEnd frontEnd = ReceiverFrontEnd.Unknown,
        string buildVersion = "",
        int queueCapacity = 4096)
    {
        Connection = new ConnectionTracker(frontEnd, buildVersion);
        _queue = new PacketSampleQueue(queueCapacity);
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "PocketMic.Analytics",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
    }

    // ---- hot path: called from the audio receive loop -------------------------------------

    /// <summary>A new-sequence packet was accepted. <paramref name="levelDbfs"/> may be <see cref="float.NaN"/> if not measured for this packet.</summary>
    public void RecordDelivered(int bufferMilliseconds, float levelDbfs, bool gateActive) =>
        Enqueue(PacketOutcome.Delivered, 1, ClampBufferMs(bufferMilliseconds), ClampLevel(levelDbfs), gateActive);

    /// <summary>Same sequence as the packet already accepted (see <see cref="PacketOutcome.Duplicate"/>).</summary>
    public void RecordDuplicate() =>
        Enqueue(PacketOutcome.Duplicate, 1, PacketSample.NoBufferMs, PacketSample.NoLevel, false);

    /// <summary>An older packet arrived after a newer one already played.</summary>
    public void RecordReordered() =>
        Enqueue(PacketOutcome.Reordered, 1, PacketSample.NoBufferMs, PacketSample.NoLevel, false);

    /// <summary><paramref name="gapCount"/> is the number of sequence numbers skipped, concealed or not.</summary>
    public void RecordLost(int gapCount) =>
        Enqueue(PacketOutcome.Lost, (short)Math.Clamp(gapCount, 1, short.MaxValue), PacketSample.NoBufferMs, PacketSample.NoLevel, false);

    /// <summary>Malformed length/header/tag/sample-rate, or an auth failure.</summary>
    public void RecordRejected() =>
        Enqueue(PacketOutcome.Rejected, 1, PacketSample.NoBufferMs, PacketSample.NoLevel, false);

    /// <summary>A buffered frame was discarded to claw back latency.</summary>
    public void RecordTrimmed(int bufferMilliseconds) =>
        Enqueue(PacketOutcome.Trimmed, 1, ClampBufferMs(bufferMilliseconds), PacketSample.NoLevel, false);

    private void Enqueue(PacketOutcome outcome, short count, short bufferMs, short levelCentiDbfs, bool gateActive)
    {
        var sample = new PacketSample(Stopwatch.GetTimestamp(), outcome, count, bufferMs, levelCentiDbfs, gateActive);
        _queue.TryEnqueue(sample);
    }

    private static short ClampBufferMs(int ms) => (short)Math.Clamp(ms, 0, short.MaxValue);

    private static short ClampLevel(float dbfs) =>
        float.IsNaN(dbfs) ? PacketSample.NoLevel : (short)Math.Clamp(Math.Round(dbfs * 100.0), short.MinValue + 1, short.MaxValue);

    // ---- low frequency: called from control channel / fault handling ----------------------

    public void RecordDiscoveryEvent(string kind, string detail = "") =>
        RaiseEventLogged(new SessionEventRecord(DateTimeOffset.UtcNow, kind, detail));

    public void RecordFault(string message) =>
        RaiseEventLogged(new SessionEventRecord(DateTimeOffset.UtcNow, "fault", message));

    /// <summary>
    /// Writes the connection summary out as a single event line, so a session file carries the
    /// link's story as well as the audio's. Emitted from <see cref="FlushFinal"/> and formatted
    /// here rather than in a report writer, because the file has to stand alone: the JSONL is
    /// what gets attached to a bug report, often without any tool to render it.
    /// </summary>
    private void RecordConnectionSummary()
    {
        var summary = Connection.Snapshot();
        var firstPacket = summary.TimeToFirstPacketMs is { } ms
            ? $"{ms:F0} ms"
            : "never";

        RaiseEventLogged(new SessionEventRecord(
            DateTimeOffset.UtcNow,
            "connection-summary",
            $"front end {summary.FrontEnd} {summary.BuildVersion}; attempts {summary.Attempts}; " +
            $"first authenticated packet {firstPacket}; reconnects {summary.ReconnectCount}; " +
            $"silence episodes {summary.SilenceEpisodes} totalling {summary.TotalSilenceMs:F0} ms " +
            $"(longest {summary.LongestSilenceMs:F0} ms); discovery failures {summary.DiscoveryFailures}; " +
            $"version mismatches {summary.VersionMismatches}"));
    }

    /// <summary>
    /// A subscriber's own bug must never propagate back into the caller — <see cref="RecordFault"/>
    /// is typically called from inside a receiver fault handler, and an exception escaping from
    /// here would replace that fault with a different, more confusing one.
    /// </summary>
    private void RaiseEventLogged(SessionEventRecord evt)
    {
        try { EventLogged?.Invoke(evt); }
        catch (Exception) { }
    }

    // ---- background thread ------------------------------------------------------------------

    private void RunLoop()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            Drain();
            CheckRollovers();
            token.WaitHandle.WaitOne(DrainInterval);
        }

        // Final drain so a stop that lands mid-interval does not lose the last packets.
        Drain();
    }

    private void Drain()
    {
        while (_queue.TryDequeue(out var sample))
        {
            double? interarrivalMs = null;
            if (sample.Outcome is PacketOutcome.Delivered or PacketOutcome.Duplicate or PacketOutcome.Reordered)
            {
                if (_lastArrivalTicks.HasValue)
                {
                    var deltaTicks = sample.TimestampTicks - _lastArrivalTicks.Value;
                    interarrivalMs = deltaTicks * 1000.0 / Stopwatch.Frequency;
                }

                _lastArrivalTicks = sample.TimestampTicks;
            }

            _tenSecond.Add(sample, interarrivalMs);
            _oneMinute.Add(sample, interarrivalMs);
            _session.Add(sample, interarrivalMs);
        }
    }

    private void CheckRollovers()
    {
        var now = DateTimeOffset.UtcNow;

        if (now - _tenSecondStart >= TenSecondSpan)
        {
            var stats = _tenSecond.Build("10s", now, _queue.DroppedCount);
            _tenSecond.Reset(now);
            _tenSecondStart = now;
            RaiseWindowClosed(stats);
        }

        if (now - _oneMinuteStart >= OneMinuteSpan)
        {
            var stats = _oneMinute.Build("1m", now, _queue.DroppedCount);
            _oneMinute.Reset(now);
            _oneMinuteStart = now;
            RaiseWindowClosed(stats);

            // The session window never resets — this is a fresh cumulative snapshot, not a
            // new window — so "whole session" numbers are visible while the session is still
            // running, not just after it ends.
            RaiseWindowClosed(_session.Build("session", now, _queue.DroppedCount));
        }
    }

    /// <summary>
    /// Runs on the background thread. A subscriber's exception (a disposed control, a bad
    /// format string) must never kill this thread — that would silently stop all analytics,
    /// including persistence, for the rest of the session.
    /// </summary>
    private void RaiseWindowClosed(WindowStats stats)
    {
        try { WindowClosed?.Invoke(stats); }
        catch (Exception) { }
    }

    /// <summary>
    /// Forces every window to emit one last snapshot immediately, including whatever partial
    /// data the current 10 s/1 min window holds. Call this right before stopping the receiver —
    /// otherwise up to the last 10 s (audio) or 1 min (session cumulative) of a run is never
    /// written anywhere, because it is still sitting in an open window when the process stops
    /// listening for <see cref="WindowClosed"/>.
    /// </summary>
    public void FlushFinal()
    {
        Drain();
        var now = DateTimeOffset.UtcNow;
        RaiseWindowClosed(_tenSecond.Build("10s", now, _queue.DroppedCount));
        RaiseWindowClosed(_oneMinute.Build("1m", now, _queue.DroppedCount));
        RaiseWindowClosed(_session.Build("session", now, _queue.DroppedCount));
        RecordConnectionSummary();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _thread.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }
}
