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
public sealed class OpusDecoder : IOpusDecoder
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
    private static extern int opus_decode(
        IntPtr decoder,
        byte[]? data, int dataLen,
        byte[] pcm, int frameSize,
        int decodeFec);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
    private static extern void opus_decoder_destroy(IntPtr decoder);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_decoder_ctl(IntPtr decoder, int request, int value);

    // -- Managed wrapper ---------------------------------------------------

    private IntPtr _handle;

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
    public int Decode(ReadOnlySpan<byte> opusData, short[] pcmBuffer)
    {
        if (_handle == IntPtr.Zero) return 0;

        byte[] data = opusData.Length > 0
            ? opusData.ToArray()
            : Array.Empty<byte>();

        int samples = opus_decode(
            _handle,
            data,
            data.Length,
            pcmBuffer,
            MaxFrameSize,
            0);

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
    /// IOpusDecoder: decodes one Opus frame into PCM16 output bytes.
    /// </summary>
    public bool TryDecode(byte[] opusData, int length, byte[] pcmOutput)
    {
        if (_handle == IntPtr.Zero || length <= 0) return false;

        int samples = opus_decode(
            _handle,
            opusData, length,
            pcmOutput, MaxFrameSize,
            0);

        return samples > 0;
    }

    /// <summary>
    /// IOpusDecoder: generates a concealment frame using Opus PLC.
    /// Opus internally models the vocal tract and produces a smoother
    /// continuation than simple waveform repetition.
    /// </summary>
    public bool TryGeneratePlc(byte[] pcmOutput)
    {
        if (_handle == IntPtr.Zero) return false;

        // Pass null data to opus_decode to trigger PLC mode.
        int samples = opus_decode(
            _handle,
            null, 0,
            pcmOutput, MaxFrameSize,
            0);

        return samples > 0;
    }

    /// <summary>
    /// IOpusDecoder: resets the decoder's internal state.
    /// </summary>
    public void Reset()
    {
        if (_handle != IntPtr.Zero)
        {
            opus_decoder_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        var err = opus_decoder_create(SampleRate, Channels, out var handle);
        if (err == 0 && handle != IntPtr.Zero)
        {
            _handle = handle;
        }
    }

    /// <summary>
    /// Releases the native decoder. Must be called when the stream stops.
    /// </summary>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            opus_decoder_destroy(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
