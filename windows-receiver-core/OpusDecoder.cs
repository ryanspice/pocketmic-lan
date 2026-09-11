using System.Runtime.InteropServices;

namespace PocketMicReceiver;

/// <summary>
/// P/Invoke wrapper around libopus for decoding Opus packets on the receiver.
///
/// The Windows receiver needs only the decoder side: each incoming Opus frame is
/// decoded to PCM16 mono at 48 kHz so the rest of the audio pipeline (jitter
/// buffer, concealment, voice processing, NAudio playback) is unchanged.
///
/// libopus is loaded from "opus.dll" (or "libopus.so" on non-Windows, though
/// this build targets Windows). The DLL must be in the application directory or
/// the system PATH. It can be built from the Opus source with the MSVC or MinGW
/// toolchain, or downloaded as a prebuilt from the xiph.org releases.
/// </summary>
public static class OpusDecoder
{
    private const string LibOpus = "opus";

    // -- Opus constants ----------------------------------------------------

    public const int SampleRate = 48_000;
    public const int Channels = 1;
    public const int MaxFrameSize = 480; // 10 ms at 48 kHz

    // -- Native declarations -----------------------------------------------

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_decoder_create(
        int sampleRate, int channels, out IntPtr decoder);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_decode(
        IntPtr decoder,
        byte[] data, int dataLen,
        short[] pcm, int frameSize,
        int decodeFec);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
    private static extern void opus_decoder_destroy(IntPtr decoder);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_decoder_ctl(IntPtr decoder, int request);

    // -- Managed wrapper ---------------------------------------------------

    private readonly IntPtr _handle;

    /// <summary>
    /// Creates an Opus decoder targeting 48 kHz mono VOIP.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The native library could not initialise the decoder.
    /// </exception>
    public OpusDecoder()
    {
        var err = opus_decoder_create(SampleRate, Channels, out var handle);
        if (err != 0 || handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"opus_decoder_create failed with error code {err}. " +
                "Ensure opus.dll is in the application directory.");
        }

        _handle = handle;
    }

    /// <summary>
    /// Decodes one Opus frame to PCM16 mono samples.
    /// </summary>
    /// <param name="opusData">The encrypted payload bytes (after GCM decryption).</param>
    /// <param name="pcmBuffer">
    /// Pre-allocated buffer for the decoded PCM16 output.
    /// Must be at least <see cref="MaxFrameSize"/> samples (960 bytes for mono 16-bit).
    /// </param>
    /// <returns>
    /// The number of samples written to <paramref name="pcmBuffer"/>, or 0 on error.
    /// </returns>
    public int Decode(ReadOnlySpan<byte> opusData, short[] pcmBuffer)
    {
        if (_handle == IntPtr.Zero) return 0;

        // opus_decode expects a mutable byte array, so copy from the span.
        byte[] data = opusData.Length > 0
            ? opusData.ToArray()
            : Array.Empty<byte>();

        int samples = opus_decode(
            _handle,
            data,
            data.Length,
            pcmBuffer,
            MaxFrameSize,
            0); // 0 = no FEC recovery

        return samples > 0 ? samples : 0;
    }

    /// <summary>
    /// Decodes one Opus frame and returns the PCM16 bytes (little-endian, mono).
    /// </summary>
    public byte[] DecodeToBytes(ReadOnlySpan<byte> opusData)
    {
        var pcm = new short[MaxFrameSize];
        int samples = Decode(opusData, pcm);
        if (samples <= 0) return Array.Empty<byte>();

        var bytes = new byte[samples * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, samples * 2);
        return bytes;
    }

    /// <summary>
    /// Releases the native decoder. Must be called when the stream stops.
    /// </summary>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            opus_decoder_destroy(_handle);
        }
    }
}
