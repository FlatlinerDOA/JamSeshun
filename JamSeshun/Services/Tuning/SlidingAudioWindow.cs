namespace JamSeshun.Services.Tuning;

/// <summary>
/// Shared windowing pipeline used by <see cref="ChordTrackerService"/> and
/// <see cref="AsrLyricTrackerService"/>: lazily resamples capture audio to a target
/// rate, fills a fixed-size window, and slides it forward by a hop/stride once full.
/// Extracted so both trackers share one implementation of resampler lifecycle, window
/// fill/slide, and RMS gating — see <c>docs/PerformanceAudioTracking.md</c>.
///
/// <para>Not thread-safe — feed it from a single analysis thread, same as
/// <see cref="AudioResampler"/>.</para>
/// </summary>
public sealed class SlidingAudioWindow
{
    private readonly int targetSampleRate;
    private readonly int windowSize;
    private readonly int hopSize;
    private readonly float[] window;
    private AudioResampler? resampler;
    private int buffered;

    public SlidingAudioWindow(int targetSampleRate, int windowSize, int hopSize)
    {
        this.targetSampleRate = targetSampleRate;
        this.windowSize = windowSize;
        this.hopSize = hopSize;
        this.window = new float[windowSize];
    }

    /// <summary>
    /// Resamples <paramref name="frame"/> to the target rate (recreating the resampler
    /// if the input rate changes), fills the internal window, and invokes
    /// <paramref name="onWindow"/> once per full window before sliding forward by the
    /// hop size.
    ///
    /// <para>
    /// <paramref name="onWindow"/> receives the INTERNAL window buffer — the same array
    /// on every call. Callers that need the data beyond the call must copy it (e.g.
    /// <c>ToArray()</c>) before returning.
    /// </para>
    /// </summary>
    public void Process(AudioFrame frame, Action<float[]> onWindow)
    {
        if (this.resampler == null || this.resampler.InputRate != frame.SampleRate)
        {
            this.resampler = new AudioResampler(frame.SampleRate, this.targetSampleRate);
        }

        var samples = this.resampler.Resample(frame.Samples);
        int offset = 0;
        while (offset < samples.Length)
        {
            int take = Math.Min(samples.Length - offset, this.windowSize - this.buffered);
            samples.AsSpan(offset, take).CopyTo(this.window.AsSpan(this.buffered));
            this.buffered += take;
            offset += take;

            if (this.buffered == this.windowSize)
            {
                onWindow(this.window);

                // Slide the window forward by one hop.
                this.window.AsSpan(this.hopSize).CopyTo(this.window);
                this.buffered = this.windowSize - this.hopSize;
            }
        }
    }

    /// <summary>Root-mean-square amplitude, used as a cheap energy/VAD gate.</summary>
    public static float Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        return (float)Math.Sqrt(sum / samples.Length);
    }
}
