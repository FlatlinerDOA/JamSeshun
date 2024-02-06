using System.Buffers;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// Code adapted from https://www.codeproject.com/Articles/32172/FFT-Guitar-Tuner
/// License: MIT
/// https://www.codeproject.com/script/Articles/ViewDownloads.aspx?aid=32172
/// </summary>
public sealed class FftPitchDetector
{
    private const float minimumFrequency = 60f; //75f; //50.0f;
    private const float maximumFrequency = 1300f; //335.0f; //500.0f;
    private const int BufferSeconds = 3;

    private readonly int sampleRate;

    public FftPitchDetector(int sampleRate)
    {
        this.sampleRate = sampleRate;
        var maxPeriod = (float)this.sampleRate / minimumFrequency;
        this.SampleBufferSize = ((int)Math.Ceiling(maxPeriod)).SmallestPow2() * (2 * BufferSeconds);
    }

    public int SampleBufferSize { get; init; }

    public DetectedPitch DetectPitch(ReadOnlySpan<float> signal)
    {
        var frequency = (float)this.FindFundamentalFrequency(signal);
        var n = Note.GetClosestNote(frequency, minimumFrequency, maximumFrequency);
        if (n.Name != null)
        {
            return new DetectedPitch(frequency, n, n.GetCentsError(frequency));
        }

        return default;
    }

    public double FindFundamentalFrequency(ReadOnlySpan<float> signal)
    {
        var spectr = FftAlgorithm.Calculate(signal);

        int usefullMinSpectr = Math.Max(0, (int)(minimumFrequency * spectr.Length / sampleRate));
        int usefullMaxSpectr = Math.Min(spectr.Length, (int)(maximumFrequency * spectr.Length / sampleRate) + 1);

        // find peaks in the FFT frequency bins 
        const int PeaksCount = 5;
        var peakIndices = FindPeaks(spectr, usefullMinSpectr, usefullMaxSpectr - usefullMinSpectr, PeaksCount);

        if (peakIndices.IndexOf(usefullMinSpectr) >= 0)
        {
            // lowest useful frequency bin shows active
            // looks like is no detectable sound, return 0
            return default;
        }

        // select fragment to check peak values: data offset
        const int verifyFragmentOffset = 0;
        // ... and half length of data
        int verifyFragmentLength = (int)(sampleRate / minimumFrequency);

        // trying all peaks to find one with smaller difference value
        double minPeakValue = double.PositiveInfinity;
        int minPeakIndex = 0;
        int minOptimalInterval = 0;
        for (int i = 0; i < peakIndices.Length; i++)
        {
            int index = peakIndices[i];
            int binIntervalStart = spectr.Length / (index + 1), binIntervalEnd = spectr.Length / index;

            // scan bins frequencies/intervals
            var (interval, peakValue) = ScanSignalIntervals(signal, verifyFragmentOffset, verifyFragmentLength, binIntervalStart, binIntervalEnd);
            if (peakValue < minPeakValue)
            {
                minPeakValue = peakValue;
                minPeakIndex = index;
                minOptimalInterval = interval;
            }
        }

        return (double)this.sampleRate / minOptimalInterval;
    }

    private static (int optimalInterval, double optimalValue) ScanSignalIntervals(
        ReadOnlySpan<float> samples,
        int index,
        int length,
        int intervalMin, int intervalMax)
    {
        double optimalValue = double.PositiveInfinity;
        int optimalInterval = 0;

        // distance between min and max range value can be big
        // limiting it to the fixed value
        const int MaxAmountOfSteps = 30;
        int steps = intervalMax - intervalMin;
        if (steps > MaxAmountOfSteps)
        {
            steps = MaxAmountOfSteps;
        }
        else if (steps <= 0)
        {
            steps = 1;
        }

        // trying all intervals in the range to find one with
        // smaller difference in signal waves
        for (int i = 0; i < steps; i++)
        {
            int interval = intervalMin + (intervalMax - intervalMin) * i / steps;

            double sum = 0;
            for (int j = 0; j < length; j++)
            {
                double diff = samples[index + j] - samples[index + j + interval];
                sum += diff * diff;
            }

            if (optimalValue > sum)
            {
                optimalValue = sum;
                optimalInterval = interval;
            }
        }

        return (optimalInterval, optimalValue);
    }

    private static ReadOnlySpan<int> FindPeaks(ReadOnlySpan<double> values, int index, int length, int peaksCount)
    {
        var peakValuesBuffer = ArrayPool<double>.Shared.Rent(peaksCount);
        var peakValues = peakValuesBuffer.AsSpan()[..peaksCount];
        int[] peakIndices = new int[peaksCount];
        
        for (int i = 0; i < peaksCount; i++)
        {
            peakValues[i] = values[peakIndices[i] = i + index];
        }

        // find min peaked value
        double minStoredPeak = peakValues[0];
        int minIndex = 0;
        for (int i = 1; i < peaksCount; i++)
        {
            if (minStoredPeak > peakValues[i]) minStoredPeak = peakValues[minIndex = i];
        }

        for (int i = peaksCount; i < length; i++)
        {
            if (minStoredPeak < values[i + index])
            {
                // replace the min peaked value with bigger one
                peakValues[minIndex] = values[peakIndices[minIndex] = i + index];

                // and find min peaked value again
                minStoredPeak = peakValues[minIndex = 0];
                for (int j = 1; j < peaksCount; j++)
                {
                    if (minStoredPeak > peakValues[j]) minStoredPeak = peakValues[minIndex = j];
                }
            }
        }

        ArrayPool<double>.Shared.Return(peakValuesBuffer);
        return peakIndices;
    }

}
