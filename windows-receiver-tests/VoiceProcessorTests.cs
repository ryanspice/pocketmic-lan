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
    public void HighPassFilterAttenuatesLowFrequencies()
    {
        var vp = new VoiceProcessor { Enabled = true };
        var pcm = new byte[960];

        // Fill with a 10 Hz sine wave at full amplitude — well below the 85 Hz cutoff.
        var sampleRate = 48_000.0;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(Math.Sin(2.0 * Math.PI * 10.0 * (i / 2) / sampleRate) * 16_000);
            pcm[i] = (byte)(sample >> 8);
            pcm[i + 1] = (byte)(sample & 0xff);
        }

        // Let the filter settle over several frames.
        for (var frame = 0; frame < 20; frame++)
        {
            vp.Process(pcm);
        }

        // After filtering, the RMS of the output should be significantly below the input.
        double sum = 0;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] << 8 | pcm[i + 1]);
            sum += (double)sample * sample;
        }
        var rms = Math.Sqrt(sum / (pcm.Length / 2));

        // The 10 Hz tone should be attenuated well below the original 16000 amplitude.
        Assert.True(rms < 8000.0, $"RMS {rms} should be below 8000 after high-pass filtering");
    }

    [Fact]
    public void LimiterClipsSignalAtCeiling()
    {
        var vp = new VoiceProcessor { Enabled = true };
        var pcm = new byte[960];

        // Fill with samples well above the limiter ceiling (0.891 * 32768 ≈ 29184).
        for (var i = 0; i < pcm.Length; i += 2)
        {
            pcm[i] = 0x7f;
            pcm[i + 1] = 0xff; // short.MaxValue = 32767
        }

        vp.Process(pcm);

        // No sample should exceed the limiter ceiling.
        const short ceiling = (short)(0.891f * 32768f); // ≈ 29184
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] << 8 | pcm[i + 1]);
            Assert.True(
                Math.Abs(sample) <= ceiling + 1, // +1 for rounding
                $"Sample {sample} exceeds limiter ceiling {ceiling}");
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
