using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// Smoke tests for the Opus decoder (requires opus.dll in the output directory).
/// These tests verify the native library loads and the P/Invoke layer works.
/// </summary>
public class OpusSmokeTest
{
    [Fact]
    public void Decoder_Creates_Successfully()
    {
        using var decoder = new OpusDecoder();
        // If we get here without exception, the native library loaded and
        // opus_decoder_create succeeded.
    }

    [Fact]
    public void Decoder_PLC_Without_State_Returns_True()
    {
        using var decoder = new OpusDecoder();
        var pcm = new byte[AudioPipeline.PacketPcmBytes];
        bool result = decoder.TryGeneratePlc(pcm);
        Assert.True(result); // Opus PLC returns silence on fresh decoder
    }

    [Fact]
    public void Decoder_Reset_Succeeds()
    {
        using var decoder = new OpusDecoder();
        decoder.Reset();
        // Should not throw
    }

    [Fact]
    public void Decoder_PLC_After_Reset_Returns_True()
    {
        using var decoder = new OpusDecoder();
        decoder.Reset();
        var pcm = new byte[AudioPipeline.PacketPcmBytes];
        bool result = decoder.TryGeneratePlc(pcm);
        Assert.True(result); // Opus PLC returns silence after reset
    }

    [Fact]
    public void Decoder_TryDecode_Minimal_Frame()
    {
        using var decoder = new OpusDecoder();
        var pcm = new byte[AudioPipeline.PacketPcmBytes];

        // Minimal Opus TOC: SILK-only, narrowband, 10ms, 1 frame
        // This is a degenerate frame; opus_decode may return samples or error.
        // The test verifies it does not crash or corrupt memory.
        var fakeFrame = new byte[] { 0x48, 0x00 };
        decoder.TryDecode(fakeFrame, fakeFrame.Length, pcm);
        // Don't assert on return value — the frame is invalid,
        // but the call must not throw or crash.
    }

    [Fact]
    public void Decoder_Dispose_Is_Safe_To_Call_Twice()
    {
        var decoder = new OpusDecoder();
        decoder.Dispose();
        decoder.Dispose(); // Double-dispose must not throw
    }
}
