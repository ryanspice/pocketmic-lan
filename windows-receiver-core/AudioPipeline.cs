using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PocketMicReceiver;

/// <summary>Authenticated packet metadata needed to order a stream before stateful decode.</summary>
public readonly record struct AudioPacketInfo(
    ulong SessionId,
    uint Sequence,
    int SampleRate,
    AudioPipeline.Codec Codec,
    int PayloadLength);

/// <summary>
/// The packet and audio mathematics of the receiver, with no UI, no sockets and no state that
/// outlives a call. Everything here is deterministic and directly testable, which matters
/// because these are the parts that decide how the stream actually sounds.
///
/// Extracted from the WinForms form so the WinUI 3 front end can share it unchanged, and so
/// concealment and trim behaviour can be unit tested rather than only judged by ear.
/// </summary>
public static class AudioPipeline
{
    public const int HeaderSize = 24;
    public const int TagSize = 16;
    public const int SampleRate = 48_000;
    public const int PacketPcmBytes = 960;
    public const int DatagramBytes = HeaderSize + PacketPcmBytes + TagSize;

    /// <summary>
    /// Measured on real hardware: interarrival p95 31 ms, p99 32 ms, worst case 234-282 ms.
    /// A 40 ms prebuffer discarded everything past the first jitter spike and turned each
    /// discard into an inserted silence frame, which is what "staticy" sounded like.
    /// </summary>
    public const int DefaultPrebufferMilliseconds = 100;

    /// <summary>
    /// The trim threshold that goes with <see cref="DefaultPrebufferMilliseconds"/>. It must be
    /// whatever <see cref="HighWaterFor"/> computes for that prebuffer: the engine starts from
    /// this constant and then recomputes it the first time a buffer setting is applied, so a
    /// different value here is a threshold that silently changes underneath the user between
    /// startup and the first slider read.
    /// </summary>
    public const int DefaultHighWaterMilliseconds = 220;

    /// <summary>Longest run of missing packets worth concealing before falling back to silence.</summary>
    public const int MaxConcealedGapPackets = 20;

    /// <summary>Roughly -46 dBFS: below normal speech, above a typical room noise floor.</summary>
    public const float TrimSilenceRms = 0.005f;

    public const byte ProtocolVersion = 1;
    public const byte ProtocolVersionV2 = 2;
    public const byte FlagEncrypted = 1;
    public const byte FlagOpus = 2;

    /// <summary>Protocol v2 header is 4 bytes larger (adds payload length at offset 24).</summary>
    public const int HeaderSizeV2 = 28;

    /// <summary>Maximum Opus frame size at 48 kHz mono, 48 kbit/s. Opus cannot exceed 512 bytes.</summary>
    public const int MaxOpusPayloadBytes = 512;

    private static ReadOnlySpan<byte> Magic => "PMIC"u8;

    /// <summary>
    /// The codec carried in a packet.
    /// </summary>
    public enum Codec { Pcm, Opus }

    /// <summary>
    /// Determines the header size and codec from the version byte in a datagram.
    /// </summary>
    public static (int HeaderSize, Codec Codec) HeaderInfo(byte[] data)
    {
        if (data.Length < 5 || !data.AsSpan()[..4].SequenceEqual(Magic))
            return (0, Codec.Pcm);

        var version = data[4];
        return version switch
        {
            ProtocolVersionV2 when data.Length >= HeaderSizeV2 &&
                (data[5] & FlagOpus) != 0 => (HeaderSizeV2, Codec.Opus),
            ProtocolVersion => (HeaderSize, Codec.Pcm),
            _ => (0, Codec.Pcm),
        };
    }

    /// <summary>
    /// Validates and decrypts one datagram. Length is checked before anything else so malformed
    /// or unrelated traffic never reaches header parsing or the AEAD.
    ///
    /// Supports both protocol v1 (PCM, fixed 1000-byte datagram) and protocol v2
    /// (Opus, variable-length datagram). When the packet is v2+Opus, the Opus payload
    /// is decoded to PCM16 and written to the <paramref name="pcm"/> buffer.
    /// </summary>
    public static bool TryDecrypt(
        AesGcm aes,
        byte[] data,
        byte[] nonce,
        byte[] pcm,
        out ulong sessionId,
        out uint sequence,
        out int sampleRate,
        IOpusDecoder? opusDecoder = null,
        byte[]? opusBuffer = null)
    {
        sessionId = 0;
        sequence = 0;
        sampleRate = 0;

        var isV2 = data.Length >= 5 && data[4] == ProtocolVersionV2;
        var authenticatedPayload = isV2 ? (opusBuffer ?? new byte[MaxOpusPayloadBytes]) : pcm;
        if (!TryAuthenticate(aes, data, nonce, authenticatedPayload, out var packet)) return false;

        sessionId = packet.SessionId;
        sequence = packet.Sequence;
        sampleRate = packet.SampleRate;
        if (packet.Codec == Codec.Pcm) return true;
        return pcm.Length >= PacketPcmBytes && opusDecoder is not null &&
            opusDecoder.TryDecode(authenticatedPayload, packet.PayloadLength, pcm);
    }

    /// <summary>
    /// Validates packet structure and authenticates/decrypts its payload without invoking a
    /// stateful codec. The caller must apply session and sequence policy before Opus decode.
    /// </summary>
    public static bool TryAuthenticate(
        AesGcm aes,
        byte[] data,
        byte[] nonce,
        byte[] payload,
        out AudioPacketInfo packet)
    {
        packet = default;

        var span = data.AsSpan();
        if (span.Length < 5 || !span[..4].SequenceEqual(Magic) || nonce.Length < 12) return false;

        var version = span[4];
        if (version == ProtocolVersion)
        {
            if (data.Length != DatagramBytes || payload.Length < PacketPcmBytes) return false;
            if (span[5] != FlagEncrypted ||
                BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6, 2)) != HeaderSize) return false;

            var sessionId = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(8, 8));
            var sequence = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(16, 4));
            var sampleRate = BinaryPrimitives.ReadInt32BigEndian(span.Slice(20, 4));
            WriteNonce(nonce, sessionId, sequence);

            try
            {
                aes.Decrypt(
                    nonce,
                    span.Slice(HeaderSize, PacketPcmBytes),
                    span.Slice(HeaderSize + PacketPcmBytes, TagSize),
                    payload.AsSpan(0, PacketPcmBytes),
                    span[..HeaderSize]);
            }
            catch (CryptographicException)
            {
                return false;
            }

            packet = new AudioPacketInfo(sessionId, sequence, sampleRate, Codec.Pcm, PacketPcmBytes);
            return true;
        }

        if (version != ProtocolVersionV2 || data.Length < HeaderSizeV2 + TagSize) return false;

        // Version 2 currently defines only encrypted Opus packets. Unknown flags are rejected.
        var flags = span[5];
        if (flags != (FlagEncrypted | FlagOpus)) return false;
        if (BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6, 2)) != HeaderSizeV2) return false;

        var v2SessionId = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(8, 8));
        var v2Sequence = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(16, 4));
        var v2SampleRate = BinaryPrimitives.ReadInt32BigEndian(span.Slice(20, 4));
        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(span.Slice(24, 4));

        if (payloadLength <= 0 ||
            payloadLength > MaxOpusPayloadBytes ||
            payloadLength > payload.Length ||
            data.Length != HeaderSizeV2 + payloadLength + TagSize) return false;

        WriteNonce(nonce, v2SessionId, v2Sequence);

        try
        {
            aes.Decrypt(
                nonce,
                span.Slice(HeaderSizeV2, payloadLength),
                span.Slice(HeaderSizeV2 + payloadLength, TagSize),
                payload.AsSpan(0, payloadLength),
                span[..HeaderSizeV2]);
        }
        catch (CryptographicException)
        {
            return false;
        }

        packet = new AudioPacketInfo(v2SessionId, v2Sequence, v2SampleRate, Codec.Opus, payloadLength);
        return true;
    }

    private static void WriteNonce(byte[] nonce, ulong sessionId, uint sequence)
    {
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(0, 8), sessionId);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8, 4), sequence);
    }

    /// <summary>
    /// Reads the protocol version out of a datagram carrying the PocketMic audio magic, without
    /// touching the AEAD.
    ///
    /// <see cref="TryDecrypt"/> already refuses a version it does not speak before decrypting,
    /// exactly so a version problem is never misreported as a pairing key problem. But refusing
    /// silently leaves the receiver unable to say which problem it was, and "rejected 6,000
    /// datagrams" reads as a key mismatch to everyone who sees it. This is how it says so.
    /// </summary>
    public static bool TryPeekProtocolVersion(byte[] data, out byte version)
    {
        version = 0;
        if (data.Length < 5) return false;

        var span = data.AsSpan();
        if (!span[..4].SequenceEqual(Magic)) return false;

        version = span[4];
        return true;
    }

    /// <summary>
    /// Distance from the expected sequence, using modular unsigned arithmetic so the 32-bit
    /// rollover at 0xFFFFFFFF is handled without a discontinuity. Negative means the packet is
    /// older than what has already been played, which covers both genuine reordering and the
    /// duplicate copies that dual-path redundancy would produce.
    /// </summary>
    public static int SequenceDelta(uint sequence, uint lastSequence) =>
        unchecked((int)(sequence - (lastSequence + 1)));

    /// <summary>
    /// Builds a replacement frame for a packet that never arrived. Substituting digital silence
    /// leaves a hard edge in the waveform and a run of those is heard as crackle; repeating the
    /// last good frame at a decaying level fades into the gap instead. With no previous frame
    /// there is nothing to repeat, so it falls back to silence.
    /// </summary>
    public static void BuildConcealmentFrame(byte[] target, byte[] lastGood, bool haveLastGood, int gapIndex)
    {
        if (!haveLastGood)
        {
            Array.Clear(target);
            return;
        }

        var gain = 1f - ((gapIndex + 1) / (float)MaxConcealedGapPackets);
        if (gain <= 0f)
        {
            Array.Clear(target);
            return;
        }

        var source = MemoryMarshal.Cast<byte, short>(lastGood);
        var destination = MemoryMarshal.Cast<byte, short>(target);
        for (var index = 0; index < source.Length; index++)
        {
            destination[index] = (short)Math.Clamp(source[index] * gain, short.MinValue, short.MaxValue);
        }
    }

    /// <summary>
    /// Exponentially smoothed level of the incoming audio. Attack is faster than release so a
    /// pause has to be genuinely sustained before latency trimming is allowed to resume.
    /// </summary>
    public static float SmoothedRms(float previous, byte[] pcm)
    {
        var samples = MemoryMarshal.Cast<byte, short>(pcm);
        double sum = 0;
        for (var index = 0; index < samples.Length; index++)
        {
            double value = samples[index];
            sum += value * value;
        }

        var rms = (float)(Math.Sqrt(sum / samples.Length) / 32768.0);
        var coefficient = rms > previous ? 0.5f : 0.05f;
        return previous + ((rms - previous) * coefficient);
    }

    /// <summary>
    /// Whether a buffered packet may be discarded to claw back latency.
    ///
    /// Dropping a 10 ms frame is audible as a click when it lands mid-syllable, and inaudible
    /// during a pause. So trimming is normally allowed only while the input is quiet — unless
    /// the backlog has grown past a hard ceiling, where accumulated latency matters more than
    /// one artefact. In practice this took observed trims from 483 per session down to 2.
    /// </summary>
    public static bool ShouldTrim(double bufferedMilliseconds, int highWaterMilliseconds, float recentRms)
    {
        var hardCeiling = highWaterMilliseconds + 160;
        if (bufferedMilliseconds > hardCeiling) return true;
        return recentRms < TrimSilenceRms && bufferedMilliseconds > highWaterMilliseconds;
    }

    /// <summary>High-water mark derived from the chosen prebuffer, kept in one place.</summary>
    public static int HighWaterFor(int prebufferMilliseconds) =>
        Math.Max(prebufferMilliseconds * 2, prebufferMilliseconds + 120);
}
