using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// End-to-end tests for <see cref="ChordTrackerService"/>: synthesised 44.1 kHz chord
/// audio in, advance-only <see cref="ChordTrackingUpdate"/>s out.  Uses
/// <see cref="Scheduler.Immediate"/> so the whole pipeline (resample → chroma →
/// template match → advance logic) runs synchronously in the test.
/// </summary>
public class ChordTrackerServiceSpecification : IDisposable
{
    private const int CaptureRate = 44100;

    private readonly FakeAudioCaptureService capture = new();
    private readonly ChordTrackerService service;
    private readonly BehaviorSubject<int> position = new(0);
    private readonly List<ChordTrackingUpdate> updates = [];

    public ChordTrackerServiceSpecification()
    {
        this.service = new ChordTrackerService(this.capture, () => Scheduler.Immediate);
    }

    public void Dispose()
    {
        this.service.Dispose();
        this.position.Dispose();
    }

    private static PerformanceLine Line(params string[] chords) =>
        new(string.Empty,
            chords.Select(c => new Chord(c, ChordLibrary.Lookup(c))).ToList(),
            string.Empty);

    private IDisposable Track(params PerformanceLine[] lines) =>
        this.Track(capo: 0, lines);

    private IDisposable Track(int capo, params PerformanceLine[] lines) =>
        this.service
            .TrackChords(FakeAudioCaptureService.Device, lines, this.position, capo)
            .Subscribe(this.updates.Add);

    private void Play(string chord, double seconds = 1.5, int capoSemitones = 0) =>
        this.capture.Push(AudioSynthesizer.Chord(chord, seconds, CaptureRate, capoSemitones), CaptureRate);

    [Fact]
    public void PlayingTheNextLinesChord_Advances()
    {
        using var subscription = this.Track(Line("G"), Line("C"), Line("D"));

        this.Play("C");

        var update = Assert.Single(this.updates.DistinctBy(u => u.LineIndex));
        Assert.Equal(1, update.LineIndex);
        Assert.Equal("C", update.DetectedChord);
        Assert.True(update.Confidence >= 0.6f);
    }

    [Fact]
    public void PlayingTheCurrentLinesChord_DoesNotAdvance()
    {
        using var subscription = this.Track(Line("G"), Line("C"));

        this.Play("G", seconds: 3.0);

        Assert.Empty(this.updates);
    }

    [Fact]
    public void ChordSharedByCurrentAndNextLine_IsNoEvidenceOfProgression()
    {
        // Every Wonderwall line contains Em7 — playing it must not run away forward.
        using var subscription = this.Track(Line("Em7", "G"), Line("Em7", "G"), Line("C"));

        this.Play("Em7", seconds: 3.0);
        this.Play("G", seconds: 3.0);

        Assert.Empty(this.updates);
    }

    [Fact]
    public void ProgressingThroughTheSong_AdvancesLineByLine()
    {
        using var subscription = this.Track(Line("G"), Line("C"), Line("D"));

        this.Play("C");
        this.Play("D");

        Assert.Equal([1, 2], this.updates.Select(u => u.LineIndex).Distinct().ToList());
    }

    [Fact]
    public void Silence_NeverAdvances()
    {
        using var subscription = this.Track(Line("G"), Line("C"));

        this.capture.Push(new float[CaptureRate * 3], CaptureRate);

        Assert.Empty(this.updates);
    }

    [Fact]
    public void ManualNavigationBackwards_RetargetsTheTracker()
    {
        using var subscription = this.Track(Line("G"), Line("C"), Line("D"));

        this.Play("C");
        Assert.Contains(this.updates, u => u.LineIndex == 1);

        // User taps Prev back to the first line; playing C should advance again.
        this.updates.Clear();
        this.position.OnNext(0);
        this.Play("C");

        Assert.Contains(this.updates, u => u.LineIndex == 1);
    }

    [Fact]
    public void OnLastLine_NeverEmits()
    {
        this.position.OnNext(1);
        using var subscription = this.Track(Line("G"), Line("C"));

        this.Play("G", seconds: 2.0);
        this.Play("C", seconds: 2.0);

        Assert.Empty(this.updates);
    }

    [Fact]
    public void WithCapo_PlayingTheNextLinesTransposedChord_StillAdvances()
    {
        // Lines are written as they'd appear in the tab (the shape to finger); the
        // capo transposes what's actually heard.  Playing the capo-transposed sound
        // for the NEXT line's written chord must still advance — proving the whole
        // pipeline (device → resample → chroma → template match → advance-only gate)
        // works end-to-end with a capo.
        using var subscription = this.Track(capo: 1, Line("G"), Line("C"), Line("D"));

        this.Play("C", capoSemitones: 1);

        var update = Assert.Single(this.updates.DistinctBy(u => u.LineIndex));
        Assert.Equal(1, update.LineIndex);
        Assert.Equal("C", update.DetectedChord);
        Assert.True(update.Confidence >= 0.6f);
    }

    [Fact]
    public void DisposingTheSubscription_StopsCapture()
    {
        var subscription = this.Track(Line("G"), Line("C"));
        Assert.Equal(1, this.capture.ActiveCaptures);

        subscription.Dispose();

        Assert.Equal(0, this.capture.ActiveCaptures);
    }
}
