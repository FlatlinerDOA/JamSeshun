namespace JamSeshun.Services.Tuning;

/// <summary>
/// Smooths a stream of raw per-window pitch detections into a stable reading.
///
/// Raw detection jumps around badly at low signal levels (the autocorrelator
/// locks onto random periodicity in background noise) and flickers between
/// adjacent notes / octaves near boundaries. This applies:
///
///  * a <b>confidence gate</b> — windows below a signal-level floor, or where
///    the detected frames don't agree, are treated as no-pitch;
///  * <b>note hysteresis</b> — once locked onto a note, a different note must
///    persist for several windows before the display switches;
///  * <b>frequency smoothing</b> — an exponential moving average within a held
///    note so the needle glides instead of jittering.
///
/// Feed it the raw frames collected over one time window (e.g. 300ms).
/// </summary>
public sealed class PitchStabilizer
{
    private readonly float minSignalLevel;
    private readonly float minAgreement;
    private readonly float emaAlpha;
    private readonly int switchConfirmations;
    private readonly int releaseWindows;

    private Note lockedNote;
    private bool hasLock;
    private float smoothedFrequency;
    private Note pendingNote;
    private int pendingCount;
    private int lowConfidenceStreak;

    /// <param name="minSignalLevel">RMS floor below which a window is treated as noise (≈ -40 dBFS).</param>
    /// <param name="minAgreement">Fraction of pitched frames that must share the dominant note.</param>
    /// <param name="emaAlpha">Smoothing factor for the held-note frequency (0=frozen, 1=no smoothing).</param>
    /// <param name="switchConfirmations">Windows a new note must persist before replacing the lock.</param>
    /// <param name="releaseWindows">Consecutive weak windows before the lock is released to "no pitch".</param>
    public PitchStabilizer(
        float minSignalLevel = 0.01f,
        float minAgreement = 0.5f,
        float emaAlpha = 0.3f,
        int switchConfirmations = 2,
        int releaseWindows = 3)
    {
        this.minSignalLevel = minSignalLevel;
        this.minAgreement = minAgreement;
        this.emaAlpha = emaAlpha;
        this.switchConfirmations = switchConfirmations;
        this.releaseWindows = releaseWindows;
    }

    public StabilizedPitch Process(IList<DetectedPitch> windowFrames)
    {
        float avgLevel = windowFrames.Count > 0 ? windowFrames.Average(f => f.SignalLevel) : 0f;
        var pitched = windowFrames.Where(f => f.Fundamental.Name != null).ToList();

        if (avgLevel >= minSignalLevel && pitched.Count > 0)
        {
            var group = pitched.GroupBy(f => f.Fundamental).OrderByDescending(g => g.Count()).First();
            float agreement = (float)group.Count() / pitched.Count;

            if (agreement >= minAgreement)
            {
                lowConfidenceStreak = 0;
                UpdateLock(group.Key, group.Average(f => f.EstimatedFrequency));

                // Confidence saturates a little above the floor; scaled by agreement.
                float levelConfidence = Math.Min(1f, avgLevel / (minSignalLevel * 4f));
                float confidence = levelConfidence * agreement;
                return Current(confidence);
            }
        }

        // Weak / unconfident window.
        if (++lowConfidenceStreak >= releaseWindows)
        {
            hasLock = false;
            pendingCount = 0;
            return default; // no pitch
        }

        // Hold the existing lock through brief dropouts (confidence reported as 0).
        return hasLock ? Current(0f) : default;
    }

    /// <summary>Resets all state. Call when (re)starting a tuning session.</summary>
    public void Reset()
    {
        hasLock = false;
        pendingCount = 0;
        lowConfidenceStreak = 0;
        smoothedFrequency = 0;
    }

    private void UpdateLock(Note detectedNote, float detectedFrequency)
    {
        if (!hasLock)
        {
            hasLock = true;
            lockedNote = detectedNote;
            smoothedFrequency = detectedFrequency;
            pendingNote = detectedNote;
            pendingCount = 0;
        }
        else if (detectedNote == lockedNote)
        {
            smoothedFrequency += emaAlpha * (detectedFrequency - smoothedFrequency);
            pendingCount = 0;
        }
        else
        {
            // A different note — require it to persist before switching the lock.
            if (detectedNote == pendingNote)
            {
                pendingCount++;
            }
            else
            {
                pendingNote = detectedNote;
                pendingCount = 1;
            }

            if (pendingCount >= switchConfirmations)
            {
                lockedNote = detectedNote;
                smoothedFrequency = detectedFrequency;
                pendingCount = 0;
            }
        }
    }

    private StabilizedPitch Current(float confidence) =>
        new(lockedNote, smoothedFrequency, lockedNote.GetCentsError(smoothedFrequency), confidence);
}
