using System.Runtime.InteropServices;

namespace PocketMicReceiver;

/// <summary>
/// Voice conditioning applied to decoded PCM before it reaches the output buffer.
///
/// This runs on the PC rather than the phone on purpose: the stream is currently lossless
/// PCM16, so nothing is gained by processing before transmission, and the phone's 10 ms
/// capture loop and battery stay untouched. It also means the chain can be retuned without
/// rebuilding and reinstalling the APK.
///
/// Chain order matters and follows normal broadcast practice:
///   DC blocker → high-pass → noise gate (downward expander) → presence EQ
///   → compressor → brick-wall limiter
///
/// Removing rumble before the gate stops low-frequency energy from holding the gate open;
/// compressing before limiting means the limiter only catches occasional peaks instead of
/// working continuously, which is what keeps it from sounding pumped.
///
/// Frames are 480 samples at 48 kHz — deliberately the same framing RNNoise expects, so a
/// native denoiser can be inserted at <see cref="DenoiseHook"/> without reshaping anything.
/// </summary>
public sealed class VoiceProcessor
{
    private const float SampleRate = 48_000f;

    private readonly float[] _scratch = new float[960];

    // Biquad state
    private float _hpX1, _hpX2, _hpY1, _hpY2;
    private float _presenceX1, _presenceX2, _presenceY1, _presenceY2;
    private float _dcX1, _dcY1;

    // Coefficients
    private float _hpB0, _hpB1, _hpB2, _hpA1, _hpA2;
    private float _prB0, _prB1, _prB2, _prA1, _prA2;

    // Dynamics state
    private float _noiseFloor = 0.002f;
    private float _gateGain = 1f;
    private float _compGain = 1f;
    private bool _gateOpen = true;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the expander is currently holding the signal down, i.e. the input is being
    /// treated as background rather than speech. Exposed because diagnostics otherwise has to
    /// guess at it from the trim path's quiet flag, which answers a different question — that
    /// flag is a smoothed level against a fixed threshold, while this is the gate's own decision
    /// after noise-floor tracking. Half gain is the midpoint of the expander's travel, so it
    /// reads as "closing or closed" without chattering around the edges of a word.
    /// </summary>
    public bool GateActive => _gateGain < 0.5f;

    /// <summary>0 = off, 1 = maximum. Scales gate depth and compression together.</summary>
    public float Strength { get; set; } = 0.6f;

    /// <summary>
    /// When true, the individual parameters below are used instead of deriving everything from
    /// <see cref="Strength"/>. Set by the phone's Custom mode.
    /// </summary>
    public bool UseCustom { get; set; }

    public float GateAmount { get; set; } = 0.6f;

    public float CompressorAmount { get; set; } = 0.6f;

    public float MakeupAmount { get; set; } = 0.6f;

    private float EffectiveGate => UseCustom ? GateAmount : Strength;

    private float EffectiveCompressor => UseCustom ? CompressorAmount : Strength;

    private float EffectiveMakeup => UseCustom ? MakeupAmount : Strength;

    /// <summary>Applies a settings push from the phone. Filters are redesigned only on change.</summary>
    public void Apply(DspConfig config)
    {
        Enabled = config.Enabled;
        UseCustom = true;
        GateAmount = Math.Clamp(config.Gate, 0f, 1f);
        CompressorAmount = Math.Clamp(config.Compressor, 0f, 1f);
        MakeupAmount = Math.Clamp(config.Makeup, 0f, 1f);

        var hz = Math.Clamp(config.HighPassHz, 20, 400);
        if (Math.Abs(hz - _highPassHz) > 0.5f)
        {
            _highPassHz = hz;
            DesignHighPass(hz);
        }

        var presence = Math.Clamp(config.PresenceDb, 0f, 12f);
        if (Math.Abs(presence - _presenceDb) > 0.05f)
        {
            _presenceDb = presence;
            DesignPresence(3_000f, presence, 1.0f);
        }
    }

    private float _highPassHz = 85f;
    private float _presenceDb = 3.5f;

    /// <summary>
    /// Optional native denoiser (e.g. RNNoise) operating in place on a 480-sample float frame
    /// scaled to +/-32768. Null until a native library is supplied.
    /// </summary>
    public Action<float[], int>? DenoiseHook { get; set; }

    public VoiceProcessor()
    {
        DesignHighPass(85f);
        DesignPresence(3_000f, 3.5f, 1.0f);
    }

    /// <summary>Processes one packet of interleaved mono PCM16 in place.</summary>
    public void Process(byte[] pcm)
    {
        if (!Enabled) return;

        var samples = MemoryMarshal.Cast<byte, short>(pcm);
        var count = samples.Length;
        if (count > _scratch.Length) return;

        for (var i = 0; i < count; i++) _scratch[i] = samples[i];

        DcBlock(_scratch, count);
        Biquad(_scratch, count, _hpB0, _hpB1, _hpB2, _hpA1, _hpA2,
            ref _hpX1, ref _hpX2, ref _hpY1, ref _hpY2);

        DenoiseHook?.Invoke(_scratch, count);

        Gate(_scratch, count);
        Biquad(_scratch, count, _prB0, _prB1, _prB2, _prA1, _prA2,
            ref _presenceX1, ref _presenceX2, ref _presenceY1, ref _presenceY2);
        Compress(_scratch, count);
        Limit(_scratch, count);

        for (var i = 0; i < count; i++)
        {
            samples[i] = (short)Math.Clamp(_scratch[i], short.MinValue, short.MaxValue);
        }
    }

    /// <summary>Removes any DC offset, which otherwise eats headroom before the limiter.</summary>
    private void DcBlock(float[] buffer, int count)
    {
        const float r = 0.9995f;
        for (var i = 0; i < count; i++)
        {
            var x = buffer[i];
            var y = x - _dcX1 + (r * _dcY1);
            _dcX1 = x;
            _dcY1 = y;
            buffer[i] = y;
        }
    }

    /// <summary>
    /// Tracks the noise floor from the quietest recent frames and expands downward below a
    /// margin above it. A gentle expander is used rather than a hard gate because hard gates
    /// chop the start of quiet consonants.
    /// </summary>
    private void Gate(float[] buffer, int count)
    {
        double sum = 0;
        for (var i = 0; i < count; i++) sum += buffer[i] * (double)buffer[i];
        var rms = (float)(Math.Sqrt(sum / count) / 32768.0);

        // Floor tracks downward quickly and upward very slowly, so speech cannot drag it up.
        _noiseFloor += rms < _noiseFloor
            ? (rms - _noiseFloor) * 0.10f
            : (rms - _noiseFloor) * 0.0005f;

        // The ceiling matters more than it looks. Measured on this hardware, an empty room sits
        // near -46 dBFS and ordinary speech only reaches about -43 dBFS RMS. The previous
        // ceiling of 0.05 (-26 dBFS) put the open threshold at -16.5 dBFS once the floor drifted
        // up - roughly 27 dB above the voice it was supposed to let through. With speakers
        // playing nearby the slow upward tracker climbs steadily, so the gate would eventually
        // close on the speaker rather than on the noise. 0.01 is about -40 dBFS: still above a
        // quiet room, still well below speech.
        _noiseFloor = Math.Clamp(_noiseFloor, 0.00002f, 0.01f);

        // Hysteresis. A single threshold makes the gate reopen and reclose within a word, which
        // is heard as the start of syllables being chewed off. Open decisively, then hold open
        // until the level falls well back down.
        var openAt = _noiseFloor * 3.0f;
        var closeAt = _noiseFloor * 1.5f;
        if (rms > openAt) _gateOpen = true;
        else if (rms < closeAt) _gateOpen = false;

        var reference = _gateOpen ? closeAt : openAt;
        var target = rms > reference ? 1f : Math.Max(1f - EffectiveGate, rms / Math.Max(reference, 1e-6f));
        target = Math.Clamp(target, 0f, 1f);

        // Fast open, slow close: catches word onsets without chattering between words.
        var coefficient = target > _gateGain ? 0.6f : 0.06f;
        for (var i = 0; i < count; i++)
        {
            _gateGain += (target - _gateGain) * coefficient;
            buffer[i] *= _gateGain;
        }
    }

    /// <summary>Soft-knee downward compression above roughly -18 dBFS.</summary>
    private void Compress(float[] buffer, int count)
    {
        var threshold = 0.125f * 32768f;
        var ratio = 1f + (3f * EffectiveCompressor);

        for (var i = 0; i < count; i++)
        {
            var magnitude = Math.Abs(buffer[i]);
            var desired = 1f;
            if (magnitude > threshold)
            {
                var over = magnitude / threshold;
                desired = (float)(Math.Pow(over, (1.0 / ratio) - 1.0));
            }

            var coefficient = desired < _compGain ? 0.25f : 0.02f;
            _compGain += (desired - _compGain) * coefficient;
            buffer[i] *= _compGain;
        }

        // Recover some of the level the compressor removed.
        var makeUp = 1f + (0.6f * EffectiveMakeup);
        for (var i = 0; i < count; i++) buffer[i] *= makeUp;
    }

    /// <summary>Hard ceiling just under full scale so nothing ever wraps or clips digitally.</summary>
    private static void Limit(float[] buffer, int count)
    {
        const float ceiling = 0.891f * 32768f; // about -1 dBFS
        for (var i = 0; i < count; i++)
        {
            if (buffer[i] > ceiling) buffer[i] = ceiling;
            else if (buffer[i] < -ceiling) buffer[i] = -ceiling;
        }
    }

    private static void Biquad(
        float[] buffer, int count,
        float b0, float b1, float b2, float a1, float a2,
        ref float x1, ref float x2, ref float y1, ref float y2)
    {
        for (var i = 0; i < count; i++)
        {
            var x = buffer[i];
            var y = (b0 * x) + (b1 * x1) + (b2 * x2) - (a1 * y1) - (a2 * y2);
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            buffer[i] = y;
        }
    }

    /// <summary>Second-order Butterworth high-pass; removes handling rumble and plosives.</summary>
    private void DesignHighPass(float frequency)
    {
        var w0 = 2.0 * Math.PI * frequency / SampleRate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2.0 * 0.707);
        var a0 = 1 + alpha;

        _hpB0 = (float)(((1 + cos) / 2) / a0);
        _hpB1 = (float)((-(1 + cos)) / a0);
        _hpB2 = _hpB0;
        _hpA1 = (float)((-2 * cos) / a0);
        _hpA2 = (float)((1 - alpha) / a0);
    }

    /// <summary>Peaking boost around the consonant band, which is what reads as clarity.</summary>
    private void DesignPresence(float frequency, float gainDb, float q)
    {
        var a = Math.Pow(10, gainDb / 40);
        var w0 = 2.0 * Math.PI * frequency / SampleRate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2 * q);
        var a0 = 1 + (alpha / a);

        _prB0 = (float)((1 + (alpha * a)) / a0);
        _prB1 = (float)((-2 * cos) / a0);
        _prB2 = (float)((1 - (alpha * a)) / a0);
        _prA1 = (float)((-2 * cos) / a0);
        _prA2 = (float)((1 - (alpha / a)) / a0);
    }
}
