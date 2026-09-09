using System.Diagnostics;
using System.Reflection;

namespace PocketMicReceiver;

/// <summary>
/// Where a front end gets the version it reports to the phone and writes into the session log.
///
/// Read from the assembly rather than from a constant so it can never drift from what was
/// actually built — a hand-maintained version string that lies is worse than no version string,
/// because it is believed.
/// </summary>
public static class ReceiverBuild
{
    public static string VersionOf(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrEmpty(informational))
        {
            // Source-link builds append "+<commit>", which is of no use on a phone screen and
            // would eat the whole announce field on its own.
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? string.Empty;
    }
}

/// <summary>
/// Which receiver front end a session ran under. Carried in the discovery announce and in the
/// session header, because with three independently shipping parts "which one was running" is
/// the first question any connection fault raises and the hardest one to answer afterwards.
/// </summary>
public enum ReceiverFrontEnd : byte
{
    Unknown = 0,

    /// <summary>The WinForms build, self-contained and the zero-prerequisite fallback.</summary>
    Classic = 1,

    /// <summary>The WinUI 3 build.</summary>
    Modern = 2,
}

/// <summary>
/// Everything measured about the connection itself, as opposed to the audio carried over it.
///
/// The audio analytics answer "how did it sound"; these answer "why did it stop". Both are
/// needed to diagnose a session after the fact, and until now only the first existed — a
/// dropout and a two-minute outage produced numerically similar loss figures and nothing that
/// distinguished them.
/// </summary>
/// <param name="Attempts">Runs started. More than one means the run was restarted.</param>
/// <param name="TimeToFirstPacketMs">
/// Milliseconds from opening the sockets to the first packet that decrypted and authenticated,
/// or null if none ever did. This is the number that separates "the phone never reached us"
/// from "the phone reached us and the key was wrong".
/// </param>
/// <param name="ReconnectCount">Times the phone fell silent and then came back within the run.</param>
/// <param name="TotalSilenceMs">Milliseconds with no audio arriving, summed over every episode.</param>
/// <param name="SilenceEpisodes">Silence episodes, including one still open when the run ended.</param>
/// <param name="LongestSilenceMs">The worst single silence, in milliseconds.</param>
/// <param name="DiscoveryFailures">
/// Probes answered but not authenticated. From the phone's side this is a failed discovery: it
/// found a receiver that would not agree with it.
/// </param>
/// <param name="VersionMismatches">
/// Datagrams carrying the PocketMic magic but a protocol version this build does not speak.
/// Counted apart from rejections precisely so they are never reported as a key mismatch.
/// </param>
/// <param name="FrontEnd">Which front end hosted the run.</param>
/// <param name="BuildVersion">Which build of it.</param>
public sealed record ConnectionSummary(
    int Attempts,
    double? TimeToFirstPacketMs,
    int ReconnectCount,
    double TotalSilenceMs,
    int SilenceEpisodes,
    double LongestSilenceMs,
    int DiscoveryFailures,
    int VersionMismatches,
    ReceiverFrontEnd FrontEnd,
    string BuildVersion);

/// <summary>
/// Accumulates <see cref="ConnectionSummary"/> over one run.
///
/// Every method here is called from a low-frequency path — start/stop, the 500 ms control tick,
/// the probe responder — with one deliberate exception: <see cref="MarkAuthenticatedPacket"/> is
/// called from the audio receive loop. It is written so that the steady-state cost is a single
/// already-cached boolean read and nothing else, which keeps the collector's one rule intact:
/// the audio loop does not allocate, lock, or wait.
/// </summary>
public sealed class ConnectionTracker
{
    private readonly object _lock = new();
    private readonly long _startedTicks = Stopwatch.GetTimestamp();
    private readonly ReceiverFrontEnd _frontEnd;
    private readonly string _buildVersion;

    private long? _firstPacketTicks;
    private int _attempts;
    private int _reconnects;
    private int _silenceEpisodes;
    private double _totalSilenceMs;
    private double _longestSilenceMs;
    private int _discoveryFailures;
    private int _versionMismatches;
    private long? _silenceStartedTicks;

    /// <summary>
    /// Read without the lock from the audio loop. It only ever transitions false to true, and a
    /// thread that reads a stale false simply takes the locked path once more — which records
    /// the same timestamp it would have anyway, because the first writer wins inside the lock.
    /// </summary>
    private volatile bool _sawFirstPacket;

    public ConnectionTracker(ReceiverFrontEnd frontEnd, string buildVersion)
    {
        _frontEnd = frontEnd;
        _buildVersion = buildVersion;
    }

    /// <summary>A run was started. Recorded before the sockets are opened.</summary>
    public void MarkAttempt()
    {
        lock (_lock) _attempts++;
    }

    /// <summary>
    /// Called from the audio receive loop for every packet that decrypted successfully. Only the
    /// first one does any work; the rest cost one volatile read.
    /// </summary>
    public void MarkAuthenticatedPacket()
    {
        if (_sawFirstPacket) return;
        lock (_lock)
        {
            if (_firstPacketTicks.HasValue) return;
            _firstPacketTicks = Stopwatch.GetTimestamp();
        }

        _sawFirstPacket = true;
    }

    /// <summary>
    /// Audio stopped arriving. Idempotent: the control tick calls this on every pass while the
    /// silence lasts, and only the first call opens the episode.
    /// </summary>
    /// <returns>True only for the call that opened the episode, so it can be logged once.</returns>
    public bool MarkSilenceBegan()
    {
        lock (_lock)
        {
            if (_silenceStartedTicks.HasValue) return false;
            _silenceStartedTicks = Stopwatch.GetTimestamp();
            _silenceEpisodes++;
            return true;
        }
    }

    /// <summary>
    /// Audio started arriving again. Closes the open episode and counts it as a reconnect, since
    /// a phone that fell silent and came back is exactly what a successful reconnect looks like
    /// from this side of the link.
    /// </summary>
    /// <returns>The length of the episode in milliseconds, or null if none was open.</returns>
    public double? MarkSilenceEnded()
    {
        lock (_lock)
        {
            if (!_silenceStartedTicks.HasValue) return null;
            var elapsed = MillisecondsSince(_silenceStartedTicks.Value);
            _silenceStartedTicks = null;
            _reconnects++;
            _totalSilenceMs += elapsed;
            if (elapsed > _longestSilenceMs) _longestSilenceMs = elapsed;
            return elapsed;
        }
    }

    /// <summary>A probe arrived that this receiver could not authenticate.</summary>
    public void MarkDiscoveryFailure()
    {
        lock (_lock) _discoveryFailures++;
    }

    /// <summary>A datagram arrived speaking a protocol version this build does not.</summary>
    public void MarkVersionMismatch()
    {
        lock (_lock) _versionMismatches++;
    }

    /// <summary>
    /// The run so far. A silence still open when this is read is included in the totals, so a
    /// session that ended mid-outage reports the outage rather than discarding it.
    /// </summary>
    public ConnectionSummary Snapshot()
    {
        lock (_lock)
        {
            var totalSilence = _totalSilenceMs;
            var longestSilence = _longestSilenceMs;
            if (_silenceStartedTicks.HasValue)
            {
                var open = MillisecondsSince(_silenceStartedTicks.Value);
                totalSilence += open;
                if (open > longestSilence) longestSilence = open;
            }

            return new ConnectionSummary(
                Attempts: _attempts,
                TimeToFirstPacketMs: _firstPacketTicks.HasValue
                    ? (_firstPacketTicks.Value - _startedTicks) * 1000.0 / Stopwatch.Frequency
                    : null,
                ReconnectCount: _reconnects,
                TotalSilenceMs: totalSilence,
                SilenceEpisodes: _silenceEpisodes,
                LongestSilenceMs: longestSilence,
                DiscoveryFailures: _discoveryFailures,
                VersionMismatches: _versionMismatches,
                FrontEnd: _frontEnd,
                BuildVersion: _buildVersion);
        }
    }

    private static double MillisecondsSince(long ticks) =>
        (Stopwatch.GetTimestamp() - ticks) * 1000.0 / Stopwatch.Frequency;
}
