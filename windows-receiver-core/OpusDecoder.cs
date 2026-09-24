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

    // Single P/Invoke declaration — the native function is:
    //   int opus_decode(OpusDecoder *st, const unsigned char *data, opus_int32 len,
    //                   opus_int16 *pcm, int frame_size, int decode_fec);
    // data can be NULL for PLC; pcm must not be NULL.

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "opus_decoder_create")]
    private static extern IntPtr NativeDecoderCreate(
        int sampleRate, int channels, out int error);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "opus_decode")]
    private static extern int NativeDecode(
        IntPtr decoder,
        byte[]? data, int dataLen,
        short[] pcm, int frameSize,
        int decodeFec);

    [DllImport(LibOpus, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "opus_decoder_destroy")]
    private static extern void NativeDecoderDestroy(IntPtr decoder);

    // -- Managed wrapper ---------------------------------------------------

    private IntPtr _handle;
    private readonly short[] _decodeBuffer = new short[MaxFrameSize];

    /// <summary>
    /// Creates an Opus decoder targeting 48 kHz mono VOIP.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The native library could not initialise the decoder.
    /// </exception>
    public OpusDecoder()
    {
        var handle = NativeDecoderCreate(SampleRate, Channels, out var error);
        if (error != 0 || handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"opus_decoder_create failed with error code {error}. " +
                "Ensure opus.dll is in the application directory.");
        }

        _handle = handle;
    }

    /// <summary>
    /// IOpusDecoder: decodes one Opus frame into PCM16 output bytes.
    /// </summary>
    public bool TryDecode(byte[] opusData, int length, byte[] pcmOutput)
    {
        if (_handle == IntPtr.Zero || length <= 0 ||
            length > AudioPipeline.MaxOpusPayloadBytes ||
            length > opusData.Length ||
            pcmOutput.Length < AudioPipeline.PacketPcmBytes) return false;

        int samples = NativeDecode(
            _handle,
            opusData, length,
            _decodeBuffer, MaxFrameSize,
            0);

        if (samples != MaxFrameSize) return false;

        // Convert short[] to byte[] (little-endian PCM16)
        int byteCount = samples * 2;
        Buffer.BlockCopy(_decodeBuffer, 0, pcmOutput, 0, byteCount);
        return true;
    }

    /// <summary>
    /// IOpusDecoder: generates a concealment frame using Opus PLC.
    /// Passes null data to opus_decode to trigger PLC mode.
    /// </summary>
    public bool TryGeneratePlc(byte[] pcmOutput)
    {
        if (_handle == IntPtr.Zero || pcmOutput.Length < AudioPipeline.PacketPcmBytes) return false;

        int samples = NativeDecode(
            _handle,
            null, 0,
            _decodeBuffer, MaxFrameSize,
            0);

        if (samples != MaxFrameSize) return false;

        int byteCount = samples * 2;
        Buffer.BlockCopy(_decodeBuffer, 0, pcmOutput, 0, byteCount);
        return true;
    }

    /// <summary>
    /// IOpusDecoder: resets the decoder's internal state.
    /// Destroys and recreates the native decoder.
    /// </summary>
    public void Reset()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeDecoderDestroy(_handle);
            _handle = IntPtr.Zero;
        }

        var handle = NativeDecoderCreate(SampleRate, Channels, out var error);
        if (error == 0 && handle != IntPtr.Zero)
        {
            _handle = handle;
        }
    }

    /// <summary>
    /// Releases the native decoder. Must be called when the stream stops.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeDecoderDestroy(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
