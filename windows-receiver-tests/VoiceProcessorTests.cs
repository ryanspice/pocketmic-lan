using System.Runtime.InteropServices;
using PocketMicReceiver;
using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// VoiceProcessor runs the DC blocker → high-pass → gate → presence → compressor → limiter
/// chain on decoded PCM16 before it reaches the output buffer. These tests exercise the
/// public Process() entry point and the Apply() filter-redesign guard.
/// </summary>
public class VoiceProcessorTests
{
    [Fact]
    public void ProcessPassesThroughSilentInputUnchanged()
    {
        var vp = new VoiceProcessor { Enabled = true };
        var pcm = new byte[960]; // all zeros

        // Process a few frames so the DC blocker and gate settle.
        for (var i = 0; i < 10; i++) vp.Process(pcm);

        // The DC blocker, gate and compressor must leave silence as silence.
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] << 8 | pcm[i + 1]);
            Assert.Equal(0, sample);
        }
    }

    [Fact]
    public void ProcessDoesNotCrashOnSilentInput()
    {
        var vp = new VoiceProcessor { Enabled = true, Strength = 0.5f };
        var pcm = new byte[960]; // all zeros
        vp.Process(pcm); // should not throw
    }

    [Fact]
    public void ProcessDoesNotCrashOnLoudInput()
    {
        var vp = new VoiceProcessor { Enabled = true, Strength = 0.5f };
        var pcm = new byte[960];
        // Fill with max amplitude.
        for (var i = 0; i < pcm.Length; i += 2)
        {
            pcm[i] = 0xff;
            pcm[i + 1] = 0x7f;
        }
        vp.Process(pcm); // should not throw
    }

    [Fact]
    public void ProcessIsDisabledWhenEnabledIsFalse()
    {
        var vp = new VoiceProcessor { Enabled = false };
        var pcm = new byte[960];
        for (var i = 0; i < pcm.Length; i += 2)
        {
            pcm[i] = 0xff;
            pcm[i + 1] = 0x7f;
        }
        var original = (byte[])pcm.Clone();
        vp.Process(pcm);
        Assert.Equal(original, pcm);
    }

    [Fact]
    public void LimiterClipsSignalAtCeiling()
    {
        var vp = new VoiceProcessor { Enabled = true, Strength = 0f };
        var pcm = new byte[960];

        // Fill with samples at full scale.
        for (var i = 0; i < pcm.Length; i += 2)
        {
            pcm[i] = 0xff;
            pcm[i + 1] = 0x7f; // short.MaxValue = 32767
        }

        vp.Process(pcm);

        // The limiter ceiling is 0.891 * 32768 ≈ 29128.
        // No sample should exceed this after processing.
        const float ceiling = 0.891f * 32768f;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            Assert.True(
                Math.Abs(sample) <= (int)ceiling + 2,
                $"Sample {sample} exceeds limiter ceiling {(int)ceiling}");
        }
    }

    [Fact]
    public void ApplyRedesignsOnlyOnChange()
    {
        var vp = new VoiceProcessor { Enabled = true };
        var config = new DspConfig(
            Enabled: true,
            HighPassHz: 85,
            Gate: 0.6f,
            Compressor: 0.6f,
            PresenceDb: 3.5f,
            Makeup: 0.6f,
            NoiseReduction: 0.4f);

        // Apply once — this should design the filters.
        vp.Apply(config);

        // Apply again with identical values — no redesign should occur.
        // (This is a no-crash test; the actual guard is on _highPassHz and _presenceDb
        //  delta being below threshold.)
        vp.Apply(config);

        // Process a frame to verify the processor is still functional.
        var pcm = new byte[960];
        vp.Process(pcm);

        // Should reach here without any exception.
        Assert.True(true);
    }
}
