namespace PocketMicReceiver;

/// <summary>Stages session/gap predictor changes; caller commits playback/order only on success.</summary>
public static class OpusPacketStage
{
    public static bool TryDecode(IOpusDecoder active, bool fresh, int missing, byte[] payload, int length,
        byte[] pcm, byte[] lastGood, bool haveLastGood, out IOpusDecoder? replacement, out byte[][]? concealment)
    {
        replacement = null;
        concealment = null;
        if (!fresh && missing == 0) return active.TryDecode(payload, length, pcm);
        var candidate = fresh ? active.CreateFresh() : active.CreateCopy();
        if (candidate is null) return false;
        try
        {
            if (!fresh && missing > 0)
            {
                concealment = new byte[missing][];
                for (var index = 0; index < missing; index++)
                {
                    var frame = new byte[AudioPipeline.PacketPcmBytes];
                    if (!candidate.TryGeneratePlc(frame))
                        AudioPipeline.BuildConcealmentFrame(frame, lastGood, haveLastGood, index);
                    concealment[index] = frame;
                }
            }
            if (!candidate.TryDecode(payload, length, pcm)) { concealment = null; return false; }
            replacement = candidate;
            candidate = null;
            return true;
        }
        finally { candidate?.Dispose(); }
    }
}
