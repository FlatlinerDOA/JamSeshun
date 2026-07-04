using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for the 44.1 kHz → 16 kHz anti-aliased streaming resampler that feeds the
/// chord and lyric trackers.
/// </summary>
public class AudioResamplerSpecification
{
    private const int InputRate = 44100;
    private const int OutputRate = 16000;

    [Fact]
    public void OneSecondOfInput_ProducesOneSecondOfOutput()
    {
        var resampler = new AudioResampler(InputRate, OutputRate);
        var input = AudioSynthesizer.Sine(440, 0.5, 1.0, InputRate);

        var output = resampler.Resample(input);

        // Within one filter-length of exactly 16000 samples.
        Assert.InRange(output.Length, OutputRate - 64, OutputRate + 64);
    }

    [Fact]
    public void PassbandTone_SurvivesWithFrequencyAndAmplitudeIntact()
    {
        var resampler = new AudioResampler(InputRate, OutputRate);
        var input = AudioSynthesizer.Sine(1000, 0.5, 2.0, InputRate);

        var output = resampler.Resample(input);

        // Frequency via zero crossings (skip the filter warm-up at the start).
        var steady = output.AsSpan(1000);
        int crossings = 0;
        for (int i = 1; i < steady.Length; i++)
        {
            if ((steady[i - 1] < 0 && steady[i] >= 0) || (steady[i - 1] >= 0 && steady[i] < 0))
            {
                crossings++;
            }
        }

        double seconds = (double)steady.Length / OutputRate;
        double frequency = crossings / (2.0 * seconds);
        Assert.InRange(frequency, 990, 1010);

        Assert.InRange(AudioResamplerSpecification.Rms(steady), 0.30, 0.40); // sine RMS = amp/√2 ≈ 0.354
    }

    [Fact]
    public void ToneAboveOutputNyquist_IsStronglyAttenuated()
    {
        var resampler = new AudioResampler(InputRate, OutputRate);
        var input = AudioSynthesizer.Sine(18000, 0.5, 1.0, InputRate);

        var output = resampler.Resample(input);

        // 18 kHz would alias to 2 kHz without the low-pass stage; the Blackman
        // stopband should crush it (input RMS ≈ 0.354).
        Assert.True(output.Length > 1000);
        Assert.True(AudioResamplerSpecification.Rms(output.AsSpan(500)) < 0.005);
    }

    [Fact]
    public void ChunkedStreaming_ProducesSameOutputAsOneShot()
    {
        var input = AudioSynthesizer.Sine(523.25, 0.4, 0.5, InputRate);

        var oneShot = new AudioResampler(InputRate, OutputRate).Resample(input);

        var chunked = new AudioResampler(InputRate, OutputRate);
        var streamed = new List<float>();
        foreach (var frame in AudioSynthesizer.Frames(input, InputRate, 1234))
        {
            streamed.AddRange(chunked.Resample(frame.Samples));
        }

        Assert.Equal(oneShot.Length, streamed.Count);
        for (int i = 0; i < oneShot.Length; i++)
        {
            Assert.Equal(oneShot[i], streamed[i], 5);
        }
    }

    [Fact]
    public void EqualRates_PassSamplesThroughUnchanged()
    {
        var resampler = new AudioResampler(InputRate, InputRate);
        var input = new float[] { 0.1f, -0.2f, 0.3f };

        Assert.True(resampler.IsPassthrough);
        Assert.Equal(input, resampler.Resample(input));
    }

    private static float Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        return (float)Math.Sqrt(sum / samples.Length);
    }
}
