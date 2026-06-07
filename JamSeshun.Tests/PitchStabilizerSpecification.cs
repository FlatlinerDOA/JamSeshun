using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using Xunit;

namespace JamSeshun.Tests;

public class PitchStabilizerSpecification
{
    private static readonly Note noteA = Note.GetClosestNote(110f, 60f, 500f);   // A2 ~110 Hz
    private static readonly Note noteASharp = Note.GetClosestNote(116.5f, 60f, 500f);

    private static DetectedPitch Frame(Note note, float freq, float level) =>
        new(freq, note, note.GetCentsError(freq), level);

    private static DetectedPitch[] Window(Note note, float freq, float level, int count = 10)
    {
        var frames = new DetectedPitch[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = Frame(note, freq, level);
        }
        return frames;
    }

    [Fact]
    public void StrongConsistentSignal_Locks()
    {
        var s = new PitchStabilizer();
        var result = s.Process(Window(noteA, 110f, level: 0.1f));

        Assert.True(result.HasPitch);
        Assert.Equal("A", result.Note.Name);
        Assert.InRange(result.Confidence, 0.9f, 1f);
    }

    [Fact]
    public void WeakSignal_IsTreatedAsNoPitch()
    {
        var s = new PitchStabilizer(minSignalLevel: 0.01f);
        // Level well below the floor — background noise.
        var result = s.Process(Window(noteA, 110f, level: 0.002f));

        Assert.False(result.HasPitch);
        Assert.Equal(0f, result.Confidence);
    }

    [Fact]
    public void Disagreement_IsTreatedAsNoPitch()
    {
        var s = new PitchStabilizer(minAgreement: 0.5f);
        // Half A, half A# — no clear winner (agreement == 0.5 each → take A, exactly at threshold).
        // Make it clearly below threshold: 3 different notes evenly.
        var frames = new[]
        {
            Frame(noteA, 110f, 0.1f),
            Frame(noteASharp, 116.5f, 0.1f),
            Frame(Note.GetClosestNote(98f, 60f, 500f), 98f, 0.1f), // G
        };
        var result = s.Process(frames);

        Assert.False(result.HasPitch);
    }

    [Fact]
    public void NoteSwitch_RequiresConfirmationWindows()
    {
        var s = new PitchStabilizer(switchConfirmations: 2);

        // Lock onto A.
        Assert.Equal("A", s.Process(Window(noteA, 110f, 0.1f)).Note.Name);

        // One window of Bb — should still hold A (hysteresis).
        Assert.Equal("A", s.Process(Window(noteASharp, 116.5f, 0.1f)).Note.Name);

        // Second window of Bb — now switches.
        Assert.Equal("Bb", s.Process(Window(noteASharp, 116.5f, 0.1f)).Note.Name);
    }

    [Fact]
    public void SingleStraySample_DoesNotDislodgeLock()
    {
        var s = new PitchStabilizer(switchConfirmations: 2);
        s.Process(Window(noteA, 110f, 0.1f));              // lock A
        s.Process(Window(noteASharp, 116.5f, 0.1f));       // stray A# (1)
        var back = s.Process(Window(noteA, 110f, 0.1f));   // back to A resets the pending switch

        Assert.Equal("A", back.Note.Name);
    }

    [Fact]
    public void HeldNote_SmoothsFrequencyTowardNewReadings()
    {
        var s = new PitchStabilizer(emaAlpha: 0.3f);
        s.Process(Window(noteA, 110f, 0.1f));               // smoothed = 110
        var r = s.Process(Window(noteA, 114f, 0.1f));       // moves partway toward 114

        // EMA with alpha 0.3: 110 + 0.3*(114-110) = 111.2 — not jumping straight to 114.
        Assert.InRange(r.Frequency, 110.5f, 112.5f);
    }

    [Fact]
    public void SustainedSilence_ReleasesLock()
    {
        var s = new PitchStabilizer(releaseWindows: 3);
        s.Process(Window(noteA, 110f, 0.1f)); // lock

        s.Process(Window(noteA, 110f, 0.001f)); // weak 1 — holds
        Assert.True(s.Process(Window(noteA, 110f, 0.001f)).Confidence == 0f); // weak 2 — holds (conf 0)
        var released = s.Process(Window(noteA, 110f, 0.001f)); // weak 3 — releases

        Assert.False(released.HasPitch);
    }

    [Fact]
    public void BriefDropout_HoldsLock()
    {
        var s = new PitchStabilizer(releaseWindows: 3);
        s.Process(Window(noteA, 110f, 0.1f)); // lock

        var held = s.Process(Window(noteA, 110f, 0.001f)); // single weak window
        Assert.True(held.HasPitch);     // still reporting A
        Assert.Equal("A", held.Note.Name);
    }
}
