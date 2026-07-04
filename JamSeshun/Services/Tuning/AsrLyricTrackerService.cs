using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// ASR lyric tracker: capture → resample to 16 kHz → energy VAD gate → 5-second rolling
/// window with 1-second stride → <see cref="ISpeechTranscriber"/> → token-overlap
/// matching (<see cref="LyricMatcher"/>) → advance-only updates.  See
/// <see cref="ILyricTrackerService"/> for the contract and
/// <c>docs/PerformanceAudioTracking.md</c> — Strategy B for the design.
///
/// <para>
/// Transcription is asynchronous and may take longer than the 1-second stride on slow
/// engines; at most one transcription runs at a time and strides arriving while one is
/// in flight are dropped, so a slow engine degrades update frequency instead of
/// building an ever-growing queue.
/// </para>
/// </summary>
public sealed class AsrLyricTrackerService : ILyricTrackerService
{
    private const int TargetSampleRate = 16000;
    private const int WindowSeconds = 5;
    private const int StrideSeconds = 1;
    private const int WindowSize = TargetSampleRate * WindowSeconds;
    private const int StrideSize = TargetSampleRate * StrideSeconds;
    private const float MinRms = 0.003f; // energy VAD: skip silent windows entirely

    private readonly IAudioCaptureService captureService;
    private readonly ISpeechTranscriber transcriber;
    private readonly Func<IScheduler> schedulerFactory;

    public AsrLyricTrackerService(IAudioCaptureService captureService, ISpeechTranscriber transcriber)
        : this(captureService, transcriber, () => new EventLoopScheduler())
    {
    }

    /// <summary>Test seam: inject <see cref="Scheduler.Immediate"/> for synchronous processing.</summary>
    public AsrLyricTrackerService(
        IAudioCaptureService captureService,
        ISpeechTranscriber transcriber,
        Func<IScheduler> schedulerFactory)
    {
        this.captureService = captureService;
        this.transcriber = transcriber;
        this.schedulerFactory = schedulerFactory;
    }

    public IObservable<LyricTrackingUpdate> TrackLyrics(
        AudioCaptureDevice device,
        IReadOnlyList<string> lines,
        IObservable<int> position)
    {
        return Observable.Create<LyricTrackingUpdate>(observer =>
        {
            var matcher = new LyricMatcher(lines);
            var scheduler = this.schedulerFactory();
            var session = new TrackingSession(matcher, this.transcriber, scheduler, observer.OnNext);

            var subscriptions = new CompositeDisposable(
                scheduler as IDisposable ?? Disposable.Empty,
                session);

            subscriptions.Add(position
                .ObserveOn(scheduler)
                .Subscribe(session.SetPosition));

            subscriptions.Add(this.captureService
                .Capture(device)
                .ObserveOn(scheduler)
                .Subscribe(session.Process, observer.OnError));

            return subscriptions;
        });
    }

    public void Dispose()
    {
        this.transcriber.Dispose();
    }

    /// <summary>Per-subscription state; audio methods only run on the analysis scheduler.</summary>
    private sealed class TrackingSession : IDisposable
    {
        private readonly LyricMatcher matcher;
        private readonly ISpeechTranscriber transcriber;
        private readonly IScheduler scheduler;
        private readonly Action<LyricTrackingUpdate> emit;
        private readonly CancellationTokenSource disposal = new();
        private readonly SlidingAudioWindow slidingWindow = new(TargetSampleRate, WindowSize, StrideSize);
        private int transcriptionInFlight; // 0/1, guarded with Interlocked

        public TrackingSession(
            LyricMatcher matcher,
            ISpeechTranscriber transcriber,
            IScheduler scheduler,
            Action<LyricTrackingUpdate> emit)
        {
            this.matcher = matcher;
            this.transcriber = transcriber;
            this.scheduler = scheduler;
            this.emit = emit;
        }

        public void SetPosition(int index)
        {
            this.matcher.SetPosition(index);
        }

        public void Process(AudioFrame frame) =>
            this.slidingWindow.Process(frame, this.OnWindowReady);

        public void Dispose()
        {
            this.disposal.Cancel();
            this.disposal.Dispose();
        }

        private void OnWindowReady(float[] window)
        {
            if (SlidingAudioWindow.Rms(window) < MinRms)
            {
                return;
            }

            // Drop this stride if the engine is still chewing on the previous one.
            if (Interlocked.CompareExchange(ref this.transcriptionInFlight, 1, 0) != 0)
            {
                return;
            }

            // Snapshot: onWindow receives SlidingAudioWindow's internal buffer, which is
            // reused/mutated on the next slide — the transcription runs asynchronously
            // afterwards, so it must work from its own copy.
            var snapshot = window.ToArray();
            _ = this.TranscribeAndMatchAsync(snapshot);
        }

        private async Task TranscribeAndMatchAsync(float[] snapshot)
        {
            try
            {
                var fragment = await this.transcriber
                    .TranscribeAsync(snapshot, TargetSampleRate, this.disposal.Token)
                    .ConfigureAwait(false);

                if (this.disposal.IsCancellationRequested || string.IsNullOrWhiteSpace(fragment))
                {
                    return;
                }

                // Hop back onto the analysis scheduler: LyricMatcher is not thread-safe.
                this.scheduler.Schedule(fragment, (_, f) =>
                {
                    var (advanceTo, score) = this.matcher.Match(f);
                    if (advanceTo != null)
                    {
                        this.emit(new LyricTrackingUpdate(advanceTo.Value, score));
                    }

                    return Disposable.Empty;
                });
            }
            catch (OperationCanceledException)
            {
                // Tracking stopped while a transcription was in flight — expected.
            }
            finally
            {
                Interlocked.Exchange(ref this.transcriptionInFlight, 0);
            }
        }
    }
}
