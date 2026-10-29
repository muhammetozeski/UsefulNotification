using System.Text.Json.Serialization;

namespace UsefulNotification;

/// <summary>Owns the waveform, envelope and gain used to turn a sequence of actual notes into audio.</summary>
public sealed record SoundVoice
{
    /// <summary>Actual oscillator shape; initially a sine wave.</summary>
    public SoundWaveform Waveform { get; set; } = new SineWaveform();
    /// <summary>Per-note gain envelope; initially a short attack and a gentle decay.</summary>
    public SoundEnvelope Envelope { get; set; } = new();
    /// <summary>Peak linear gain from zero to one; initially 0.055.</summary>
    public double Volume { get; set; } = .055;
}
/// <summary>Controls the audible gain over each note's lifetime.</summary>
public sealed record SoundEnvelope
{
    /// <summary>Linear fade-in duration, initially five milliseconds.</summary>
    public TimeSpan Attack { get; set; } = TimeSpan.FromMilliseconds(5);
    /// <summary>Duration after Attack during which gain falls toward Sustain, initially one second.</summary>
    public TimeSpan Decay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>Gain after decay, from zero to one; initially zero.</summary>
    public double Sustain { get; set; }
    /// <summary>Final fade-out within the note duration, initially forty milliseconds.</summary>
    public TimeSpan Release { get; set; } = TimeSpan.FromMilliseconds(40);
    /// <summary>Positive decay exponent; larger values fall away sooner, initially three.</summary>
    public double DecayCurve { get; set; } = 3;
}
/// <summary>Actual oscillator data, serialized together with the voice rather than looked up by an instrument name.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "shape")]
[JsonDerivedType(typeof(SineWaveform), "sine")]
[JsonDerivedType(typeof(SquareWaveform), "square")]
[JsonDerivedType(typeof(HarmonicWaveform), "harmonic")]
public abstract record SoundWaveform;
/// <summary>A smooth single-frequency oscillator.</summary>
public sealed record SineWaveform : SoundWaveform;
/// <summary>A constant-amplitude square oscillator suitable for the three urgent beeps.</summary>
public sealed record SquareWaveform : SoundWaveform;
/// <summary>A normalized sum of actual sine partials whose frequency ratios create a bell or plucked timbre.</summary>
public sealed record HarmonicWaveform : SoundWaveform
{
    /// <summary>Actual frequency multipliers and relative amplitudes, initially the fundamental only.</summary>
    public List<SoundPartial> Partials { get; set; } = [new(1, 1)];
}
/// <summary>One actual sine component of a harmonic oscillator.</summary>
/// <param name="FrequencyRatio">Positive multiplier of the note frequency.</param>
/// <param name="Amplitude">Finite relative amplitude; the complete waveform is normalized by the sum of absolute amplitudes.</param>
public sealed record SoundPartial(double FrequencyRatio, double Amplitude);
/// <summary>Validates note data and generates in-memory, mono, sixteen-bit PCM WAV audio without external dependencies.</summary>
public static class SoundSynthesis
{
    const double MaximumSeconds = 120;

    /// <summary>Rejects invalid note, waveform, envelope and gain data before a request is accepted or previewed.</summary>
    /// <param name="pattern">Actual sound object. A file takes precedence and does not require tone steps.</param>
    public static void Validate(SoundPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.AudioFile is not null) return;
        if (pattern.Steps is null || pattern.Steps.Count == 0) throw new ArgumentException("A synthesized sound requires at least one note.");
        double duration = 0;
        foreach (ToneStep step in pattern.Steps)
        {
            if (step is null || step.Frequency is < 37 or > 32767 || step.Duration <= TimeSpan.Zero || step.Pause < TimeSpan.Zero)
                throw new ArgumentException("Tone frequencies must be 37 through 32767 hertz, with positive duration and nonnegative pause.");
            duration += (step.Duration + step.Pause).TotalSeconds;
        }
        if (duration > MaximumSeconds) throw new ArgumentOutOfRangeException(nameof(pattern), "A synthesized sound must not exceed two minutes.");
        if (pattern.Synthesis is not { } voice) return;
        if (!double.IsFinite(voice.Volume) || voice.Volume is < 0 or > 1 || voice.Envelope is not { } envelope || voice.Waveform is null)
            throw new ArgumentException("A voice requires a waveform, envelope and volume from zero to one.");
        if (envelope.Attack < TimeSpan.Zero || envelope.Decay < TimeSpan.Zero || envelope.Release < TimeSpan.Zero ||
            !double.IsFinite(envelope.Sustain) || envelope.Sustain is < 0 or > 1 || !double.IsFinite(envelope.DecayCurve) || envelope.DecayCurve <= 0)
            throw new ArgumentException("Envelope times must be nonnegative, sustain must be zero through one and decay exponent must be positive.");
        if (voice.Waveform is HarmonicWaveform harmonic && (harmonic.Partials is null || harmonic.Partials.Count == 0 ||
            harmonic.Partials.Any(partial => partial is null || !double.IsFinite(partial.FrequencyRatio) || partial.FrequencyRatio <= 0 || !double.IsFinite(partial.Amplitude)) ||
            harmonic.Partials.Sum(partial => Math.Abs(partial.Amplitude)) is not (> 0 and < double.PositiveInfinity)))
            throw new ArgumentException("A harmonic waveform requires finite partials with positive frequency ratios and a nonzero amplitude sum.");
        if (voice.Waveform is not (SineWaveform or SquareWaveform or HarmonicWaveform)) throw new ArgumentException("The oscillator type is unsupported.");
    }

    /// <summary>Generates complete PCM WAV bytes in memory; AudioFile patterns remain the native player's responsibility.</summary>
    /// <param name="pattern">Actual notes and optional synthesized voice.</param>
    /// <param name="sampleRate">Samples per second, initially 44100; supported from 8000 through 192000.</param>
    /// <returns>RIFF WAV bytes containing mono signed sixteen-bit little-endian samples.</returns>
    public static byte[] RenderWav(SoundPattern pattern, int sampleRate = 44100)
    {
        Validate(pattern);
        if (pattern.AudioFile is not null) throw new ArgumentException("An audio file is played directly rather than synthesized.", nameof(pattern));
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SoundVoice voice = pattern.Synthesis ?? new() { Waveform = new SquareWaveform(), Envelope = new() { Attack = TimeSpan.Zero, Decay = TimeSpan.Zero, Release = TimeSpan.Zero, Sustain = 1 }, Volume = .085 };
        int count = checked(pattern.Steps.Sum(step => checked((int)Math.Round(step.Duration.TotalSeconds * sampleRate) + (int)Math.Round(step.Pause.TotalSeconds * sampleRate))));
        using var memory = new MemoryStream(checked(44 + count * sizeof(short)));
        using var writer = new BinaryWriter(memory, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(checked(36 + count * sizeof(short)));
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(sampleRate); writer.Write(sampleRate * sizeof(short)); writer.Write((short)sizeof(short)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * sizeof(short));
        foreach (ToneStep step in pattern.Steps)
        {
            int sounding = (int)Math.Round(step.Duration.TotalSeconds * sampleRate);
            for (int index = 0; index < sounding; index++)
            {
                double seconds = (double)index / sampleRate;
                double sample = Oscillate(voice.Waveform, seconds * step.Frequency) * Gain(voice.Envelope, seconds, step.Duration.TotalSeconds) * voice.Volume;
                writer.Write((short)Math.Round(Math.Clamp(sample, -1, 1) * short.MaxValue));
            }
            int silence = (int)Math.Round(step.Pause.TotalSeconds * sampleRate);
            for (int index = 0; index < silence; index++) writer.Write((short)0);
        }
        writer.Flush();
        return memory.ToArray();
    }

    /// <summary>Samples an actual oscillator at a phase measured in cycles.</summary>
    static double Oscillate(SoundWaveform waveform, double cycles) => waveform switch
    {
        SquareWaveform => Math.Sin(2 * Math.PI * cycles) >= 0 ? 1 : -1,
        HarmonicWaveform harmonic => harmonic.Partials.Sum(partial => partial.Amplitude * Math.Sin(2 * Math.PI * cycles * partial.FrequencyRatio)) / harmonic.Partials.Sum(partial => Math.Abs(partial.Amplitude)),
        SineWaveform => Math.Sin(2 * Math.PI * cycles),
        _ => throw new ArgumentException("The oscillator type is unsupported.")
    };

    /// <summary>Evaluates attack, decaying sustain and final release inside one note.</summary>
    static double Gain(SoundEnvelope envelope, double seconds, double duration)
    {
        double attack = envelope.Attack.TotalSeconds, decay = envelope.Decay.TotalSeconds, release = envelope.Release.TotalSeconds;
        double gain = attack > 0 && seconds < attack ? seconds / attack :
            decay > 0 ? envelope.Sustain + (1 - envelope.Sustain) * Math.Pow(1 - Math.Clamp((seconds - attack) / decay, 0, 1), envelope.DecayCurve) : envelope.Sustain;
        return release > 0 ? gain * Math.Clamp((duration - seconds) / release, 0, 1) : gain;
    }
}
