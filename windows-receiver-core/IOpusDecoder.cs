namespace PocketMicReceiver;

/// <summary>
/// Abstraction over an Opus decoder that provides both regular decoding and
/// packet loss concealment (PLC).  When the receiver carries Opus-encoded audio
/// the engine should prefer <see cref="TryGeneratePlc"/> for lost frames rather
/// than the PCM16 waveform-substitution in <see cref="AudioPipeline.BuildConcealmentFrame"/>,
/// because Opus internally models the vocal tract and produces a smoother, more
/// natural-sounding continuation of the last decoded frame.
///
/// A null implementation (no decoder available) causes the engine to fall back to
/// PCM16 concealment transparently — the receive loop checks for null before calling.
///
/// Implementations should wrap a native Opus decoder (e.g. libopus via P/Invoke
/// or a managed binding).  The decoder must be configured for 48 kHz mono with
/// 20 ms frames (480 samples / 960 bytes of PCM16 output).
/// </summary>
public interface IOpusDecoder : IDisposable
{
    /// <summary>
    /// Decodes one Opus frame into 480 interleaved mono PCM16 samples (960 bytes).
    /// </summary>
    /// <param name="opusData">Raw Opus packet bytes.</param>
    /// <param name="length">Number of valid bytes in <paramref name="opusData"/>.</param>
    /// <param name="pcmOutput">Target buffer, must be at least
    /// <see cref="AudioPipeline.PacketPcmBytes"/> (960 bytes).</param>
    /// <returns>True if decoding succeeded and <paramref name="pcmOutput"/> was filled.</returns>
    bool TryDecode(byte[] opusData, int length, byte[] pcmOutput);

    /// <summary>
    /// Asks the Opus decoder to generate a concealment frame using its internal
    /// packet loss concealment algorithm.  This produces a smoother continuation
    /// of the last decoded frame than simple waveform repetition, because the
    /// decoder maintains internal state about the spectral envelope and pitch.
    /// </summary>
    /// <param name="pcmOutput">Target buffer, must be at least
    /// <see cref="AudioPipeline.PacketPcmBytes"/> (960 bytes).</param>
    /// <returns>True if PLC succeeded and <paramref name="pcmOutput"/> was filled;
    /// false if the decoder has no state to extrapolate from (e.g. before the first
    /// real packet was decoded).</returns>
    bool TryGeneratePlc(byte[] pcmOutput);

    /// <summary>
    /// Resets the decoder's internal state.  Called on session resync so stale
    /// codec state from a previous session does not bleed into the new one.
    /// </summary>
    void Reset();
}
