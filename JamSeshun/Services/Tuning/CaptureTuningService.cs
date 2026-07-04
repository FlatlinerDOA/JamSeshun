using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// Shared pitch-detection layer built on top of <see cref="IAudioCaptureService"/>.
/// Runs <see cref="AutoCorrelationPitchDetector"/> over the raw capture stream so every
/// platform gets tuner pitch detection for free once it implements raw capture — see
/// <c>docs/PerformanceAudioTracking.md</c>.
///
/// <para>
/// All analysis runs on a dedicated scheduler (never the capture callback or UI
/// thread), the same pattern as <see cref="ChordTrackerService"/>.
/// </para>
/// </summary>
public sealed class CaptureTuningService : ITuningService
{
    private readonly IAudioCaptureService captureService;
    private readonly Func<IScheduler> schedulerFactory;

    public CaptureTuningService(IAudioCaptureService captureService)
        : this(captureService, () => new EventLoopScheduler())
    {
    }

    /// <summary>Test seam: inject <see cref="Scheduler.Immediate"/> for synchronous analysis.</summary>
    public CaptureTuningService(IAudioCaptureService captureService, Func<IScheduler> schedulerFactory)
    {
        this.captureService = captureService;
        this.schedulerFactory = schedulerFactory;
    }

    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
        this.captureService.GetAudioCaptureDevices();

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            var scheduler = this.schedulerFactory();
            var session = new TrackingSession(observer.OnNext);

            var subscriptions = new CompositeDisposable(scheduler as IDisposable ?? Disposable.Empty);

            subscriptions.Add(this.captureService
                .Capture(device)
                .ObserveOn(scheduler)
                .Subscribe(session.Process, observer.OnError));

            return subscriptions;
        });
    }

    /// <summary>Per-subscription mutable state, only ever touched on the analysis scheduler.</summary>
    private sealed class TrackingSession
    {
        private readonly Action<DetectedPitch> emit;
        private AutoCorrelationPitchDetector? pitchDetector;
        private BipBuffer<float>? buffer;
        private int sampleRate;

        public TrackingSession(Action<DetectedPitch> emit)
        {
            this.emit = emit;
        }

        public void Process(AudioFrame frame)
        {
            if (this.pitchDetector == null || this.sampleRate != frame.SampleRate)
            {
                this.sampleRate = frame.SampleRate;
                this.pitchDetector = new AutoCorrelationPitchDetector(frame.SampleRate);
                this.buffer = new BipBuffer<float>(this.pitchDetector.SampleBufferSize * 4);
            }

            var remaining = frame.Samples.AsSpan();
            while (!remaining.IsEmpty)
            {
                var written = this.buffer!.Offer(remaining);
                remaining = remaining.Slice(written);
                while (this.buffer.Used >= this.pitchDetector.SampleBufferSize)
                {
                    var poll = this.buffer.Poll(this.pitchDetector.SampleBufferSize);
                    this.emit(this.pitchDetector.DetectPitch(poll));
                }
            }
        }
    }
}
