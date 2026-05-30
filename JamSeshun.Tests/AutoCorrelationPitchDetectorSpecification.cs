using Avalonia.Headless.XUnit;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

public class AutoCorrelationPitchDetectorSpecification
{
    // Standard guitar open-string frequencies (Hz)
    private const double LowE  = 82.41;
    private const double A     = 110.00;
    private const double D     = 146.83;
    private const double G     = 196.00;
    private const double B     = 246.94;
    private const double HighE = 329.63;

    private static AutoCorrelationPitchDetector Detector(int sampleRate = 44100) => new(sampleRate);
    private const double Tolerance = 3.0; // ±3 Hz (well within a semitone)

    // ── Sine wave sanity ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void ShouldDetectBasicSineWave()
    {
        var f = new FrequencyExample(440d, 0);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 439d, 441d);
    }

    [AvaloniaFact]
    public void ShouldDetectFundamentalFromHarmonics()
    {
        var f = new FrequencyExample(440d, 4, 0, 2);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 439d, 441d);
    }

    [AvaloniaFact]
    public void ShouldDetectFundamentalFromHarmonicsWithNoise()
    {
        var f = new FrequencyExample(440d, 4, 0.3, 2);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 438d, 442d);
    }

    [AvaloniaFact]
    public void ShouldNotDetectPureNoise()
    {
        var f = new FrequencyExample(440d, 0, 100d, 2d, Amplitude: 0.1d);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.NotInRange(actual.EstimatedFrequency, 430d, 450d);
    }

    // ── Guitar string model (weak fundamental, dominant 2nd/3rd harmonics) ─

    [AvaloniaFact]
    public void GuitarString_LowE_ShouldDetect()
    {
        var f = FrequencyExample.GuitarString(LowE);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, LowE - Tolerance, LowE + Tolerance);
    }

    [AvaloniaFact]
    public void GuitarString_A_ShouldDetect()
    {
        var f = FrequencyExample.GuitarString(A);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, A - Tolerance, A + Tolerance);
    }

    [AvaloniaFact]
    public void GuitarString_D_ShouldDetect()
    {
        var f = FrequencyExample.GuitarString(D);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, D - Tolerance, D + Tolerance);
    }

    [AvaloniaFact]
    public void GuitarString_G_ShouldDetect()
    {
        var f = FrequencyExample.GuitarString(G);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, G - Tolerance, G + Tolerance);
    }

    [AvaloniaFact]
    public void GuitarString_B_ShouldDetect()
    {
        var f = FrequencyExample.GuitarString(B);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, B - Tolerance, B + Tolerance);
    }

    [AvaloniaFact]
    public void GuitarString_HighE_ShouldDetect()
    {
        var f = FrequencyExample.GuitarString(HighE);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, HighE - Tolerance, HighE + Tolerance);
    }

    // ── Detuned strings: confirm cents error has the right sign/magnitude ──

    [AvaloniaFact]
    public void GuitarString_FlatLowE_ShouldReportNegativeCents()
    {
        // 81 Hz is ~30 cents flat of the 82.41 Hz low E (still nearest to E)
        var f = FrequencyExample.GuitarString(81.0);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.Equal("E", actual.Fundamental.Name);
        Assert.InRange(actual.EstimatedFrequency, 79.5, 82.0);
        Assert.True(actual.ErrorInCents < 0, $"expected flat (negative cents) but got {actual.ErrorInCents}");
    }

    [AvaloniaFact]
    public void GuitarString_SharpA_ShouldReportPositiveCents()
    {
        // 113 Hz is ~47 cents sharp of the 110 Hz A
        var f = FrequencyExample.GuitarString(113.0);
        var actual = Detector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.Equal("A", actual.Fundamental.Name);
        Assert.InRange(actual.EstimatedFrequency, 111.5, 114.5);
        Assert.True(actual.ErrorInCents > 0, $"expected sharp (positive cents) but got {actual.ErrorInCents}");
    }

    [AvaloniaFact]
    public void Silence_ShouldDetectNothing()
    {
        var silence = new float[44100];
        var actual = Detector().DetectPitch(silence.AsSpan());
        Assert.Null(actual.Fundamental.Name);
    }

    [AvaloniaFact]
    public void GuitarString_WithDcOffset_ShouldStillDetect()
    {
        // Real microphone input carries a DC bias / sub-bass rumble on top of the
        // signal. The detector must remove it rather than reject the frame.
        var f = FrequencyExample.GuitarString(A);
        var samples = f.Samples;
        for (int i = 0; i < samples.Length; i++)
            samples[i] += 0.4f; // constant DC offset

        var actual = Detector(f.SampleRate).DetectPitch(samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, A - Tolerance, A + Tolerance);
    }
}
