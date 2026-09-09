using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PocketMicReceiver;

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
    public const byte FlagEncrypted = 1;

    private static ReadOnlySpan<byte> Magic => "PMIC"u8;

    /// <summary>
    /// Validates and decrypts one datagram. Length is checked before anything else so malformed
    /// or unrelated traffic never reaches header parsing or the AEAD.
    /// </summary>
    public static bool TryDecrypt(
        AesGcm aes,
        byte[] data,
        byte[] nonce,
        byte[] pcm,
        out ulong sessionId,
        out uint sequence,
        out int sampleRate)
    {
        sessionId = 0;
        sequence = 0;
        sampleRate = 0;
        if (data.Length != DatagramBytes) return false;

        var span = data.AsSpan();
        if (!span[..4].SequenceEqual(Magic)) return false;

        // Protocol version, the encrypted flag, and the declared header length are all checked
        // before the AEAD is touched. A v2 sender or a plaintext frame must be rejected here
        // rather than failing later as an authentication error, which would be misdiagnosed as
        // a pairing key mismatch.
        if (span[4] != ProtocolVersion || (span[5] & FlagEncrypted) == 0) return false;
        if (BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6, 2)) != HeaderSize) return false;

        sessionId = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(8, 8));
        sequence = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(16, 4));
        sampleRate = BinaryPrimitives.ReadInt32BigEndian(span.Slice(20, 4));

        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(0, 8), sessionId);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8, 4), sequence);

        try
        {
            aes.Decrypt(
                nonce,
                span.Slice(HeaderSize, PacketPcmBytes),
                span.Slice(HeaderSize + PacketPcmBytes, TagSize),
                pcm,
                span[..HeaderSize]);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
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
