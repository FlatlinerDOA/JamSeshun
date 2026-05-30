namespace JamSeshun.Services.Tuning;

/// <summary>
/// Time-domain autocorrelation pitch detector.
///
/// FFT bin width at guitar's low frequencies is wider than a semitone
/// (≈11 Hz at the low-E string for a 4096-point FFT @ 44.1 kHz), so an
/// FFT peak alone cannot resolve tuning to cents. Autocorrelation works
/// directly in the time domain and locks onto the fundamental *period*,
/// which is robust even when the fundamental is weaker than its harmonics
/// (the normal case for a plucked guitar string).
/// </summary>
public sealed class AutoCorrelationPitchDetector : IPitchDetector
{
    private const float MinimumFrequency = 60.0f;
    private const float MaximumFrequency = 500.0f;

    // Peak must be this fraction of zero-lag energy to count as pitched (vs. noise).
    // Kept lenient: real mic input is noisier than synthetic tones, and the view
    // model already stabilizes results by grouping over 300ms windows.
    private const double PitchedThreshold = 0.20;
    // First autocorrelation peak reaching this fraction of the global max is taken
    // as the fundamental — prevents octave-down errors (picking 2T, 3T over T).
    private const double PeakRatio = 0.85;

    private readonly int sampleRate;
    private readonly int minLag;
    private readonly int maxLag;

    public AutoCorrelationPitchDetector(int sampleRate)
    {
        this.sampleRate = sampleRate;
        this.minLag = Math.Max(1, (int)(sampleRate / MaximumFrequency));
        this.maxLag = (int)(sampleRate / MinimumFrequency);

        // Window long enough for several periods of the lowest note plus the
        // longest lag we scan, rounded up to a power of two.
        this.SampleBufferSize = (this.maxLag * 4).SmallestPow2();
    }

    public int SampleBufferSize { get; }

    public DetectedPitch DetectPitch(ReadOnlySpan<float> signal)
    {
        var frequency = (float)FindFundamentalFrequency(signal);
        var note = Note.GetClosestNote(frequency, MinimumFrequency, MaximumFrequency);
        return note.Name != null
            ? new DetectedPitch(frequency, note, note.GetCentsError(frequency))
            : default;
    }

    private double FindFundamentalFrequency(ReadOnlySpan<float> signal)
    {
        int hiLag = Math.Min(maxLag, signal.Length / 2);
        if (hiLag <= minLag) return 0d;

        // Remove DC offset. Real microphone input carries DC bias and sub-bass
        // rumble that inflate the zero-lag energy and otherwise swamp the
        // correlation — synthetic test tones don't, which is why this matters
        // only on real hardware.
        double mean = 0;
        for (int i = 0; i < signal.Length; i++)
            mean += signal[i];
        mean /= signal.Length;

        var x = new double[signal.Length];
        double energy = 0;
        for (int i = 0; i < signal.Length; i++)
        {
            double v = signal[i] - mean;
            x[i] = v;
            energy += v * v;
        }

        // Silence gate (energy is on DC-removed signal, so quiet rooms pass through).
        if (energy < 1e-4) return 0d;

        // Autocorrelation across the candidate lag range.
        var corr = new double[hiLag + 2];
        double maxPeak = 0;
        for (int lag = minLag; lag <= hiLag; lag++)
        {
            double sum = 0;
            int n = signal.Length - lag;
            for (int i = 0; i < n; i++)
                sum += x[i] * x[i + lag];

            corr[lag] = sum;
            if (sum > maxPeak) maxPeak = sum;
        }

        // Reject unpitched signals. Normalized against zero-lag energy this is
        // amplitude-independent: a clean tone scores ~0.8+, broadband noise well
        // under the threshold.
        if (maxPeak <= 0 || maxPeak / energy < PitchedThreshold) return 0d;

        // Take the first local-maximum peak that reaches PeakRatio of the global
        // max. Scanning low→high lags first means we prefer the shortest period
        // (true fundamental) over its sub-octaves.
        double threshold = maxPeak * PeakRatio;
        int bestLag = 0;
        for (int lag = minLag + 1; lag < hiLag; lag++)
        {
            if (corr[lag] >= threshold &&
                corr[lag] > corr[lag - 1] &&
                corr[lag] >= corr[lag + 1])
            {
                bestLag = lag;
                break;
            }
        }

        if (bestLag == 0) return 0d;

        // Parabolic interpolation around the peak for sub-sample period accuracy.
        double y0 = corr[bestLag - 1], y1 = corr[bestLag], y2 = corr[bestLag + 1];
        double denom = y0 - 2 * y1 + y2;
        double shift = denom != 0 ? 0.5 * (y0 - y2) / denom : 0;
        double refinedLag = bestLag + shift;

        return refinedLag > 0 ? sampleRate / refinedLag : 0d;
    }
}
