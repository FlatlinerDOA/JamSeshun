namespace JamSeshun.Services.Tuning;

/// <summary>
/// Streaming anti-aliased sample-rate converter (e.g. 44.1 kHz capture → 16 kHz model
/// input).  Implements polyphase rational resampling: conceptually upsample by L,
/// low-pass filter with a windowed-sinc FIR, then decimate by M, where
/// L/M = outputRate/inputRate reduced to lowest terms (160/441 for 44.1 k → 16 k).
///
/// <para>
/// The filter cutoff sits just below the lower of the two Nyquist frequencies, so
/// content that would alias into the output band is attenuated by the Blackman-window
/// stopband (~-74 dB).  State is carried across calls, so audio can be fed in
/// arbitrarily sized blocks; this is the resampling stage required by
/// <c>docs/PerformanceAudioTracking.md</c>.
/// </para>
///
/// <para>Not thread-safe — feed it from a single processing thread.</para>
/// </summary>
public sealed class AudioResampler
{
    private const int TapsPerPhase = 32;

    private readonly int upFactor;      // L
    private readonly int downFactor;    // M
    private readonly float[] coefficients; // prototype FIR, length upFactor * TapsPerPhase
    private readonly float[] pending;   // carried input: last TapsPerPhase-1 samples + any unconsumed tail
    private int pendingCount;
    private long nextOutputIndex;       // index m of the next output sample to produce
    private long inputStartIndex;       // absolute input index of pending[0]

    public AudioResampler(int inputRate, int outputRate)
    {
        if (inputRate <= 0 || outputRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRate), "Sample rates must be positive.");
        }

        this.InputRate = inputRate;
        this.OutputRate = outputRate;

        var gcd = AudioResampler.GreatestCommonDivisor(inputRate, outputRate);
        this.upFactor = outputRate / gcd;
        this.downFactor = inputRate / gcd;
        this.coefficients = AudioResampler.DesignFilter(this.upFactor, this.downFactor);

        // History plus one incoming block; grows on demand in Resample.
        this.pending = new float[TapsPerPhase * 4];
    }

    public int InputRate { get; }

    public int OutputRate { get; }

    /// <summary>True when input and output rates match and samples pass through unchanged.</summary>
    public bool IsPassthrough => this.upFactor == 1 && this.downFactor == 1;

    /// <summary>
    /// Feeds a block of input samples and returns the output samples that became
    /// available.  Returns an empty array when more input is needed.
    /// </summary>
    public float[] Resample(ReadOnlySpan<float> input)
    {
        if (this.IsPassthrough)
        {
            return input.ToArray();
        }

        var work = this.AppendToPending(input);

        // Output m is computable when its newest required input sample q = (m*M)/L
        // is inside `work`.  Each output needs inputs q-TapsPerPhase+1 .. q.
        long available = this.inputStartIndex + work.Length;
        var produced = new List<float>((int)(input.Length * (long)this.upFactor / this.downFactor) + 2);

        while (true)
        {
            long upsampledIndex = this.nextOutputIndex * this.downFactor;
            long q = upsampledIndex / this.upFactor;
            if (q >= available)
            {
                break;
            }

            int phase = (int)(upsampledIndex % this.upFactor);
            float sum = 0f;
            for (int k = 0; k < TapsPerPhase; k++)
            {
                long inputIndex = q - k;
                if (inputIndex < this.inputStartIndex)
                {
                    break; // before recorded history — treated as silence at stream start
                }

                sum += this.coefficients[(long)k * this.upFactor + phase] * work[(int)(inputIndex - this.inputStartIndex)];
            }

            produced.Add(sum);
            this.nextOutputIndex++;
        }

        this.TrimPending(work, available);
        return produced.ToArray();
    }

    private float[] AppendToPending(ReadOnlySpan<float> input)
    {
        var work = new float[this.pendingCount + input.Length];
        this.pending.AsSpan(0, this.pendingCount).CopyTo(work);
        input.CopyTo(work.AsSpan(this.pendingCount));
        return work;
    }

    private void TrimPending(float[] work, long available)
    {
        // Keep the last TapsPerPhase-1 samples (plus anything not yet consumed) as history.
        long oldestNeeded = (this.nextOutputIndex * this.downFactor) / this.upFactor - (TapsPerPhase - 1);
        long keepFrom = Math.Max(this.inputStartIndex, oldestNeeded);
        int keepCount = (int)(available - keepFrom);

        if (keepCount > this.pending.Length)
        {
            keepCount = this.pending.Length; // never grows: TapsPerPhase*4 exceeds worst-case carry
        }

        work.AsSpan(work.Length - keepCount, keepCount).CopyTo(this.pending);
        this.pendingCount = keepCount;
        this.inputStartIndex = available - keepCount;
    }

    private static float[] DesignFilter(int upFactor, int downFactor)
    {
        int length = upFactor * TapsPerPhase;
        double center = (length - 1) / 2.0;

        // Cutoff (cyclic frequency relative to the upsampled rate): just below the
        // narrower Nyquist so the transition band straddles the fold-over point.
        double cutoff = 0.94 / (2.0 * Math.Max(upFactor, downFactor));

        var taps = new float[length];
        for (int n = 0; n < length; n++)
        {
            double x = n - center;
            double sinc = x == 0.0
                ? 2.0 * cutoff
                : Math.Sin(2.0 * Math.PI * cutoff * x) / (Math.PI * x);
            double window = 0.42
                - 0.5 * Math.Cos(2.0 * Math.PI * n / (length - 1))
                + 0.08 * Math.Cos(4.0 * Math.PI * n / (length - 1));

            // upFactor gain compensates for the zero-stuffing energy loss.
            taps[n] = (float)(sinc * window * upFactor);
        }

        return taps;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
