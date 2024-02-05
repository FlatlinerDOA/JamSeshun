using System.Collections;
using System.Diagnostics;

namespace JamSeshun.Services.Tuning;

public sealed class BitStreamAutoCorrelatedPitchDetector
{
    private static readonly Note[] NoteBaseFreqs = new Note[]
        {
            new("C", 16.35f),
            new("C#", 17.32f),
            new("D", 18.35f),
            new("Eb", 19.45f),
            new("E", 20.60f),
            new("F", 21.83f),
            new("F#", 23.12f),
            new("G", 24.50f),
            new("G#", 25.96f),
            new("A", 27.50f),
            new("Bb", 29.14f),
            new("B", 30.87f),
        };
    private readonly List<Note> allNotes;

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

        this.allNotes = (from note in NoteBaseFreqs
                         from octave in Enumerable.Range(0, 9)
                         let shiftedNote = note.ShiftOctave(octave)
                         where shiftedNote.Frequency >= minimumFrequency && shiftedNote.Frequency <= maximumFrequency
                         select shiftedNote).ToList();

        this.SampleBufferSize = SmallestPow2((int)Math.Ceiling(this.maxPeriod)) * 2;
    }

    public DetectedPitch DetectPitch(ReadOnlyMemory<float> signal)
    {
        Debug.Assert(signal.Length == this.SampleBufferSize, $"Bad buffer size of {signal.Length}, expected {this.SampleBufferSize}.");
        var zeroCrosses = ZeroCrossingBits(signal);
        var correlations = new BitStreamAcf(zeroCrosses);
        var estimatedFrequency = ProcessHarmonics(correlations);
        var n = this.GetClosestNote(estimatedFrequency);
        return new DetectedPitch(estimatedFrequency, n, GetCents(estimatedFrequency, n.Frequency));
    }

    public Note GetClosestNote(float estimatedFrequency) =>
        estimatedFrequency <= 0.0f ? default : this.allNotes.MinBy(note => Math.Abs(note.Frequency - estimatedFrequency));

    public float GetCents(float estimatedFrequency, float targetFrequency) => targetFrequency > 0.0f ? (float)(1200.0d * Math.Log2(estimatedFrequency / targetFrequency)) : 0;

    private static BitArray ZeroCrossingBits(ReadOnlyMemory<float> audioSignals)
    {
        var s = audioSignals.Span;
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

    private static int SmallestPow2(int n)
    {
        int pow = 1;
        while (pow < n)
        {
            pow *= 2;
        }

        return pow;
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
        int maxDiv = (int)(estIndex / minPeriod);
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
