using System.Reactive.Concurrency;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for <see cref="CaptureTuningService"/>: pitch detection layered on top of
/// <see cref="IAudioCaptureService"/>. Uses <see cref="Scheduler.Immediate"/> so the
/// whole pipeline (capture → buffer → <see cref="AutoCorrelationPitchDetector"/>) runs
/// synchronously in the test, matching the pattern in
/// <c>ChordTrackerServiceSpecification</c>.
/// </summary>
public class CaptureTuningServiceSpecification
{
    private const int CaptureRate = 44100;

    private readonly FakeAudioCaptureService capture = new();
    private readonly CaptureTuningService service;
    private readonly List<DetectedPitch> updates = [];

    public CaptureTuningServiceSpecification()
    {
        this.service = new CaptureTuningService(this.capture, () => Scheduler.Immediate);
    }

    private IDisposable Track() =>
        this.service
            .StartDetectingPitch(FakeAudioCaptureService.Device)
            .Subscribe(this.updates.Add);

    [Fact]
    public void PlayingA110HzTone_DetectsTheFundamentalFrequency()
    {
        using var subscription = this.Track();

        var samples = AudioSynthesizer.Sine(110.0, 0.5, 2.0, CaptureRate);
        this.capture.Push(samples, CaptureRate);

        var pitched = this.updates.Where(u => u.EstimatedFrequency > 0).ToList();
        Assert.NotEmpty(pitched);
        Assert.Contains(pitched, u => Math.Abs(u.EstimatedFrequency - 110.0) < 3.0);
    }

    [Fact]
    public void DisposingTheSubscription_StopsCapture()
    {
        var subscription = this.Track();
        Assert.Equal(1, this.capture.ActiveCaptures);

        subscription.Dispose();

        Assert.Equal(0, this.capture.ActiveCaptures);
    }

    [Fact]
    public void GetAudioCaptureDevices_DelegatesToTheCaptureService()
    {
        var devices = new List<IReadOnlyList<AudioCaptureDevice>>();
        using var subscription = this.service.GetAudioCaptureDevices().Subscribe(devices.Add);

        var result = Assert.Single(devices);
        Assert.Equal([FakeAudioCaptureService.Device], result);
    }
}
