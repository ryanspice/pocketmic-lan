namespace PocketMicReceiver;

public enum AudioPacketOrderStatus
{
    Accept,
    Late,
    CodecChangedWithinSession,
}

public readonly record struct AudioPacketOrderDecision(
    AudioPacketOrderStatus Status,
    bool NewSession,
    int MissingPackets,
    int SequenceDelta);

/// <summary>
/// Tracks authenticated packet order without changing codec state. Call Inspect after
/// authentication, decode only accepted packets, then Commit only after decode succeeds.
/// </summary>
public sealed class AudioPacketOrder
{
    private ulong? _sessionId;
    private AudioPipeline.Codec? _codec;
    private uint? _lastSequence;

    public AudioPacketOrderDecision Inspect(AudioPacketInfo packet)
    {
        if (_sessionId != packet.SessionId)
        {
            return new AudioPacketOrderDecision(AudioPacketOrderStatus.Accept, NewSession: true, MissingPackets: 0, SequenceDelta: 0);
        }

        if (_codec != packet.Codec)
        {
            return new AudioPacketOrderDecision(AudioPacketOrderStatus.CodecChangedWithinSession, NewSession: false, MissingPackets: 0, SequenceDelta: 0);
        }

        if (!_lastSequence.HasValue)
        {
            return new AudioPacketOrderDecision(AudioPacketOrderStatus.Accept, NewSession: false, MissingPackets: 0, SequenceDelta: 0);
        }

        var delta = AudioPipeline.SequenceDelta(packet.Sequence, _lastSequence.Value);
        return delta < 0
            ? new AudioPacketOrderDecision(AudioPacketOrderStatus.Late, NewSession: false, MissingPackets: 0, SequenceDelta: delta)
            : new AudioPacketOrderDecision(AudioPacketOrderStatus.Accept, NewSession: false, MissingPackets: delta, SequenceDelta: delta);
    }

    public void Commit(AudioPacketInfo packet)
    {
        _sessionId = packet.SessionId;
        _codec = packet.Codec;
        _lastSequence = packet.Sequence;
    }

    public void Reset()
    {
        _sessionId = null;
        _codec = null;
        _lastSequence = null;
    }
}
