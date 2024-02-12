using System.Collections;
using System.Diagnostics;

namespace JamSeshun.Services.Tuning;

public sealed class BitStreamAutoCorrelatedPitchDetector : IPitchDetector
{
    private const float minimumFrequency = 75f; //50.0f;
    private const float maximumFrequency = 335.0f; //500.0f;
    private readonly int sampleRate;
    private readonly float minPeriod;
    private readonly float maxPeriod;

    public int SampleBufferSize { get; }

    public BitStreamAutoCorrelatedPitchDetector(int sampleRate)
    {
        this.sampleRate = sampleRate;
        this.minPeriod = (float)this.sampleRate / maximumFrequency;
        this.maxPeriod = (float)this.sampleRate / minimumFrequency;
        this.SampleBufferSize = ((int)Math.Ceiling(this.maxPeriod)).SmallestPow2() * 2;
    }

    public DetectedPitch DetectPitch(ReadOnlySpan<float> signal)
    {
        Debug.Assert(signal.Length == this.SampleBufferSize, $"Bad buffer size of {signal.Length}, expected {this.SampleBufferSize}.");
        var zeroCrosses = ZeroCrossingBits(signal);
        var correlations = new BitStreamAcf(zeroCrosses);
        var estimatedFrequency = ProcessHarmonics(correlations);
        var n = Note.GetClosestNote(estimatedFrequency, minimumFrequency, maximumFrequency);
        return new DetectedPitch(estimatedFrequency, n, n.GetCentsError(estimatedFrequency));
    }

    private static BitArray ZeroCrossingBits(ReadOnlySpan<float> audioSignals)
    {
        var s = audioSignals;
        var zeroCrosses = new BitArray(s.Length, false); // Using byte array to store zero crossing states
        bool hasCrossed = false;
        for (var i = 0; i < s.Length; i++)
        {
            var sample = s[i];
            if (sample < -0.1f)
            {
                hasCrossed = false;
            }
            else if (sample > 0.0f)
            {
                hasCrossed = true;
            }

            zeroCrosses.Set(i, hasCrossed);
        }

        return zeroCrosses;
    }

    private float ProcessHarmonics(BitStreamAcf acf)
    {
        var correlationCounts = Enumerable.Range(0, acf.MaximumPosition)
            .Select(acf.CalculateAcf)
            .ToArray();
        var maxCount = correlationCounts.Max();
        
        // Offset by one because there will always be a minimum of 0 at the origin (it perfectly correlates with itself).
        var (estIndex, min) = correlationCounts.Skip(1).ArgMin();

        var subThreshold = 0.15 * maxCount;
        int maxDiv = (int)(estIndex / this.minPeriod);
        for (int div = maxDiv; div != 0; div--)
        {
            bool allStrong = true;
            float mul = 1.0f / div;

            for (int k = 1; k != div; k++)
            {
                int subPeriod = (int)(k * estIndex * mul);
                if (correlationCounts[subPeriod] > subThreshold)
                {
                    allStrong = false;
                    break;
                }
            }

            if (allStrong)
            {
                estIndex = (int)(estIndex * mul);
                break;
            }
        }

        // Get the start edge
        float prev = 0;
        int startIndex = 0;

        for (; startIndex < correlationCounts.Length && correlationCounts[startIndex] <= 0.0f; ++startIndex)
            prev = correlationCounts[startIndex];

        // Ensure we don't go out of bounds in the next step
        if (estIndex == 0 || estIndex - 1 >= correlationCounts.Length || startIndex >= correlationCounts.Length)
            return 0.0f; // No frequency detected

        float dy = correlationCounts[startIndex] - prev;
        float dx1 = -prev / dy;


        // Get the next edge
        int nextIndex = estIndex - 1;
        for (; nextIndex < correlationCounts.Length && correlationCounts[nextIndex] <= 0.0f; ++nextIndex)
            prev = correlationCounts[nextIndex];
        dy = correlationCounts[nextIndex] - prev;
        float dx2 = -prev / dy;

        float nSamples = (nextIndex - startIndex) + (dx2 - dx1);
        if (nSamples == 0f)
        {
            return 0f;
        }

        float estFreq = this.sampleRate / nSamples;
        ////Debug.Assert(!float.IsInfinity(estFreq), "Infinity!");
        return estFreq;
    }        
}
