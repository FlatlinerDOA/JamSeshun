using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for <see cref="AsrLyricTrackerService"/> with a scripted transcriber:
/// verifies the audio pipeline (resample → VAD gate → 5 s window / 1 s stride),
/// single-flight transcription, and advance-only matching.
/// </summary>
public class AsrLyricTrackerServiceSpecification : IDisposable
{
    private const int CaptureRate = 44100;

    private static readonly string[] lines =
    [
        "today is gonna be the day",
        "somehow you're gonna be the one that saves me",
        "and after all",
    ];

    private readonly FakeAudioCaptureService capture = new();
    private readonly ScriptedTranscriber transcriber = new();
    private readonly AsrLyricTrackerService service;
    private readonly BehaviorSubject<int> position = new(0);
    private readonly List<LyricTrackingUpdate> updates = [];

    public AsrLyricTrackerServiceSpecification()
    {
        this.service = new AsrLyricTrackerService(this.capture, this.transcriber, () => Scheduler.Immediate);
    }

    public void Dispose()
    {
        this.service.Dispose();
        this.position.Dispose();
    }

    private IDisposable Track() =>
        this.service
            .TrackLyrics(FakeAudioCaptureService.Device, AsrLyricTrackerServiceSpecification.lines, this.position)
            .Subscribe(this.updates.Add);

    /// <summary>Pushes <paramref name="seconds"/> of audible (non-silent) audio.</summary>
    private void Sing(double seconds) =>
        this.capture.Push(AudioSynthesizer.Sine(220, 0.2, seconds, CaptureRate), CaptureRate);

    [Fact]
    public void TranscriptMatchingNextLine_HeldForTwoWindows_Advances()
    {
        this.transcriber.Script("somehow you're gonna be the one that saves me");
        using var subscription = this.Track();

        this.Sing(7.5); // fills the 5 s window, then two more 1 s strides

        var update = Assert.Single(this.updates.DistinctBy(u => u.LineIndex));
        Assert.Equal(1, update.LineIndex);
        Assert.True(update.Confidence > 0.4f);
    }

    [Fact]
    public void SilentAudio_NeverReachesTheTranscriber()
    {
        this.transcriber.Script("somehow you're gonna be the one that saves me");
        using var subscription = this.Track();

        this.capture.Push(new float[CaptureRate * 8], CaptureRate);

        Assert.Equal(0, this.transcriber.CallCount);
        Assert.Empty(this.updates);
    }

    [Fact]
    public void UnrelatedTranscript_NeverAdvances()
    {
        this.transcriber.Script("something else entirely different");
        using var subscription = this.Track();

        this.Sing(10);

        Assert.Empty(this.updates);
    }

    [Fact]
    public void SlowTranscription_DropsStridesInsteadOfQueueing()
    {
        // The transcriber never completes, so exactly one call is ever in flight.
        this.transcriber.Hang();
        using var subscription = this.Track();

        this.Sing(10); // would be 6 windows if strides queued

        Assert.Equal(1, this.transcriber.CallCount);
    }

    [Fact]
    public void ManualNavigation_MovesTheMatchingWindow()
    {
        this.transcriber.Script("and after all");
        this.position.OnNext(1);
        using var subscription = this.Track();

        this.Sing(7.5);

        var update = Assert.Single(this.updates.DistinctBy(u => u.LineIndex));
        Assert.Equal(2, update.LineIndex);
    }

    [Fact]
    public void DisposingTheSubscription_StopsCapture()
    {
        var subscription = this.Track();
        Assert.Equal(1, this.capture.ActiveCaptures);

        subscription.Dispose();

        Assert.Equal(0, this.capture.ActiveCaptures);
    }

    /// <summary>Returns a fixed transcript for every window, counting calls.</summary>
    private sealed class ScriptedTranscriber : ISpeechTranscriber
    {
        private string transcript = string.Empty;
        private bool hang;

        public int CallCount { get; private set; }

        public void Script(string text) => this.transcript = text;

        public void Hang() => this.hang = true;

        public Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken)
        {
            this.CallCount++;
            Assert.Equal(16000, sampleRate);
            Assert.Equal(16000 * 5, samples.Length);

            return this.hang
                ? new TaskCompletionSource<string>().Task
                : Task.FromResult(this.transcript);
        }

        public void Dispose()
        {
        }
    }
}
