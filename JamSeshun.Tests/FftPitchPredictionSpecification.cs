using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace JamSeshun.Tests;


public class FftPitchDetectorSpecification
{
    [AvaloniaFact]
    public void ShouldDetectBasicSineWave()
    {
        var f = new FrequencyExample(440d, 0);
        RenderFrequency(f);
        var actual = new FftPitchDetector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 439d, 441d);
    }

    [AvaloniaFact]
    public void ShouldDetectBasicSineWaveForTenSeconds()
    {
        var f = new FrequencyExample(440d, 0, 0, 10);
        RenderFrequency(f);
        var actual = new FftPitchDetector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 439d, 441d);
    }

    [AvaloniaFact]
    public void ShouldDetectFundamentalFromHarmonics()
    {
        var f = new FrequencyExample(440d, 4, 0, 10);
        RenderFrequency(f);
        var actual = new FftPitchDetector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 439d, 441d);
    }

    [AvaloniaFact]
    public void ShouldDetectFundamentalFromHarmonicsWithNoise()
    {
        var f = new FrequencyExample(440d, 4, 0.5, 10);
        RenderFrequency(f);
        var actual = new FftPitchDetector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 439d, 441d);
    }

    [AvaloniaFact]
    public void ShouldDetectNoisySineWave()
    {
        var f = new FrequencyExample(440d, 0, 0.1d);
        RenderFrequency(f);
        var actual = new FftPitchDetector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 430d, 450d);
    }

    [AvaloniaFact]
    public void ShouldDetectNoisySineWaveWithShortDuration()
    {
        var f = new FrequencyExample(440d, 0, 0.1d, 0.3);
        RenderFrequency(f);
        var actual = new FftPitchDetector(f.SampleRate).DetectPitch(f.Samples.AsSpan());
        Assert.InRange(actual.EstimatedFrequency, 430d, 450d);
    }

    private static void RenderFrequency(FrequencyExample f)
    {
        var spectra = FftAlgorithm.GenerateSpectrogramData(f.Samples);
        var preview = FftAlgorithm.RenderSpectrogramToBitmap(spectra);
        using var fs = File.Create($@"D:\Temp\Fft\{f.Fundamental}-({f.HarmonicCount})-{f.SampleRate}-{f.Noise}-{f.DurationInSeconds}.png");
        preview.Save(fs);
    }
}

