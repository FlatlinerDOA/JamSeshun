using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Deterministic audio synthesis for chord-detection and resampler tests: sums a
/// small harmonic series (amplitude 1/h) for every fretted string of a chord, which
/// is a crude but effective stand-in for a strummed guitar.
/// </summary>
public static class AudioSynthesizer
{
    /// <summary>Standard-tuning open-string MIDI numbers, low E to high e.</summary>
    private static readonly int[] openStringMidi = [40, 45, 50, 55, 59, 64];

    public static float[] Sine(double frequency, double amplitude, double seconds, int sampleRate)
    {
        var samples = new float[(int)(seconds * sampleRate)];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * i / sampleRate));
        }

        return samples;
    }

    /// <summary>
    /// Synthesises a strummed chord from its fingering ([E A D G B e], -1 = muted).
    /// </summary>
    /// <param name="capoSemitones">
    /// Simulates a capo raising every open string's effective pitch by this many
    /// semitones (0 = no capo) — fretting a shape above a capo at fret N sounds like
    /// fretting that same shape N frets higher with no capo.
    /// </param>
    public static float[] Chord(int[] frets, double seconds, int sampleRate, int capoSemitones = 0)
    {
        var samples = new float[(int)(seconds * sampleRate)];
        int voices = 0;

        for (int s = 0; s < 6; s++)
        {
            if (frets[s] < 0)
            {
                continue;
            }

            voices++;
            double fundamental = 440.0 * Math.Pow(2.0, (AudioSynthesizer.openStringMidi[s] + frets[s] + capoSemitones - 69) / 12.0);
            for (int harmonic = 1; harmonic <= 5; harmonic++)
            {
                double frequency = fundamental * harmonic;
                if (frequency >= sampleRate / 2.0)
                {
                    break;
                }

                double amplitude = 1.0 / harmonic;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] += (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * i / sampleRate));
                }
            }
        }

        // Normalise to a healthy but unclipped level.
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }

        if (peak > 0f)
        {
            float scale = 0.5f / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }

        return samples;
    }

    /// <summary>Synthesises a named chord using the built-in <see cref="ChordLibrary"/> fingering.</summary>
    /// <param name="capoSemitones">
    /// Simulates a capo raising every open string's effective pitch by this many
    /// semitones (0 = no capo) — see the fingering overload for the physical model.
    /// </param>
    public static float[] Chord(string name, double seconds, int sampleRate, int capoSemitones = 0) =>
        AudioSynthesizer.Chord(
            ChordLibrary.Lookup(name) ?? throw new ArgumentException($"Unknown chord {name}", nameof(name)),
            seconds,
            sampleRate,
            capoSemitones);

    /// <summary>Splits samples into capture-sized <see cref="AudioFrame"/>s.</summary>
    public static IEnumerable<AudioFrame> Frames(float[] samples, int sampleRate, int frameSize = 4410)
    {
        for (int offset = 0; offset < samples.Length; offset += frameSize)
        {
            int take = Math.Min(frameSize, samples.Length - offset);
            yield return new AudioFrame(samples.AsSpan(offset, take).ToArray(), sampleRate);
        }
    }
}
