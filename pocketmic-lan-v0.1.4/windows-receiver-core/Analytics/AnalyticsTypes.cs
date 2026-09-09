namespace PocketMicReceiver;

/// <summary>
/// What happened to one authenticated-or-attempted datagram, as seen by the receive loop.
///
/// The duplicate/reordered split costs nothing extra to compute: today's receive loop already
/// tracks <c>_lastSequence</c> and drops everything with <c>delta &lt; 0</c> into one "late"
/// bucket. Given that state, <c>delta == -1</c> means the incoming sequence equals the packet
/// already accepted (an exact duplicate), and <c>delta &lt;= -2</c> means something older still
/// arrived after a newer packet already played (a true reorder). No extra tracking state is
/// needed — just a second branch on the delta that is already computed.
/// </summary>
public enum PacketOutcome : byte
{
    /// <summary>A new-sequence packet was decrypted, accepted, and handed to the audio buffer.</summary>
    Delivered = 0,

    /// <summary>Same sequence as the packet already accepted (<c>delta == -1</c>).</summary>
    Duplicate = 1,

    /// <summary>An older packet arrived after a newer one already played (<c>delta &lt;= -2</c>).</summary>
    Reordered = 2,

    /// <summary>
    /// A gap in the sequence was detected. <c>Count</c> on the sample carries the gap size,
    /// concealed or not — the collector does not need to know which; that decision lives in
    /// the jitter buffer.
    /// </summary>
    Lost = 3,

    /// <summary>Malformed length/header/tag/sample-rate, or an auth failure.</summary>
    Rejected = 4,

    /// <summary>A buffered frame was discarded to claw back latency (high-water trim).</summary>
    Trimmed = 5,
}

/// <summary>
/// One aggregated slice of receiver behaviour. The same shape is used for the 10 s tumbling
/// window, the 1 min tumbling window, and the whole-session cumulative snapshot — only
/// <see cref="WindowKind"/> and the time span differ, so a report or chart never needs to know
/// which cadence produced a given line.
/// </summary>
public sealed record WindowStats(
    string WindowKind,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    long PacketsReceived,
    long PacketsLost,
    long Duplicates,
    long Reordered,
    long Rejected,
    long Trimmed,
    double LossPercent,
    double InterarrivalP50Ms,
    double InterarrivalP95Ms,
    double InterarrivalP99Ms,
    double InterarrivalMaxMs,
    double InterarrivalStdevMs,
    double BufferDepthAvgMs,
    double BufferDepthMaxMs,
    double LevelDbfsAvg,
    double LevelDbfsMin,
    double LevelDbfsMax,

    // LevelSamples: how many delivered packets carried a measurable level. Digital silence has
    // no dBFS value and is excluded, so this can be zero for a window that received plenty of
    // packets — and it is the only thing distinguishing "the level averaged 0 dBFS" from
    // "nothing was measured and the average beside it is a placeholder".
    long LevelSamples,

    double NoiseGateActivePercent,
    long DroppedSamples);

/// <summary>
/// Written once at the start of a session file. Exists so two sessions recorded on different
/// days or different networks can be told apart at a glance instead of by diffing raw numbers —
/// a 250 ms tail on a phone's cellular hotspot and a 250 ms tail on the home LAN mean very
/// different things.
/// </summary>
public sealed record SessionHeader(
    DateTimeOffset StartedAt,
    string PhoneAddress,
    string PcHostName,
    string NetworkSubnet,
    string OutputDevice,
    string MonitorDevice,
    int PrebufferMilliseconds,
    int HighWaterMilliseconds,
    bool VoiceEnhanceEnabled,
    int VoiceStrength,
    string ProtocolVersion,

    // FrontEnd/BuildVersion: which front end hosted this run and which build of it. Three parts
    // now ship independently, so "it was broken that evening" is only answerable if the log
    // records which of them was actually on screen. Defaulted so an older session file that
    // predates the fields still deserializes.
    ReceiverFrontEnd FrontEnd = ReceiverFrontEnd.Unknown,
    string BuildVersion = "");

/// <summary>
/// A discovery event or fault worth remembering after the fact: peer found, key mismatch,
/// resync, device fault, and so on. Low volume by nature, so unlike <see cref="WindowStats"/>
/// these are never batched or rate-limited.
/// </summary>
public sealed record SessionEventRecord(
    DateTimeOffset At,
    string Kind,
    string Detail);
