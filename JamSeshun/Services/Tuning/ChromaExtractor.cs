namespace JamSeshun.Services.Tuning;

/// <summary>
/// Computes a 12-bin chroma (pitch-class profile) vector from a window of mono PCM
/// samples.  The window is FFT'd (Hann-windowed by <see cref="FftAlgorithm"/>), and
/// spectral magnitude between <see cref="MinFrequency"/> and <see cref="MaxFrequency"/>
/// is accumulated into the pitch class of each bin's nearest semitone.
///
/// <para>
/// This is the front end of the DSP chord detector used by
/// <see cref="ChordTrackerService"/>: template matching over chroma is orders of
/// magnitude cheaper than transformer inference, giving per-hop latencies well under
/// the audio hop interval (see <c>docs/PerformanceAudioTracking.md</c>).
/// </para>
/// </summary>
public sealed class ChromaExtractor
{
    private const double MinFrequency = 70.0;   // just below guitar low E (82 Hz)
    private const double MaxFrequency = 5000.0; // upper harmonics still useful for chroma

    private readonly int sampleRate;

    public ChromaExtractor(int sampleRate)
    {
        this.sampleRate = sampleRate;
    }

    /// <summary>
    /// Returns the L2-normalised 12-bin chroma of <paramref name="window"/>
    /// (index 0 = C … 11 = B), or an all-zero vector for silence.
    /// </summary>
    public float[] Extract(ReadOnlySpan<float> window)
    {
        var power = FftAlgorithm.Calculate(window);
        int fftLength = power.Length;
        var chroma = new float[12];

        int minBin = Math.Max(1, (int)(MinFrequency * fftLength / this.sampleRate));
        int maxBin = Math.Min(fftLength / 2 - 1, (int)(MaxFrequency * fftLength / this.sampleRate));

        for (int bin = minBin; bin <= maxBin; bin++)
        {
            double frequency = (double)bin * this.sampleRate / fftLength;
            double midi = 69.0 + 12.0 * Math.Log2(frequency / 440.0);
            int pitchClass = ((int)Math.Round(midi) % 12 + 12) % 12;

            // Magnitude (√power) compresses dynamics so a loud bass note cannot
            // completely swamp the upper chord tones.
            chroma[pitchClass] += (float)Math.Sqrt(power[bin]);
        }

        double norm = 0;
        for (int i = 0; i < 12; i++)
        {
            norm += (double)chroma[i] * chroma[i];
        }

        if (norm <= 0)
        {
            return chroma;
        }

        float scale = (float)(1.0 / Math.Sqrt(norm));
        for (int i = 0; i < 12; i++)
        {
            chroma[i] *= scale;
        }

        return chroma;
    }
}
