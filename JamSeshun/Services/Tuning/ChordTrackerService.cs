using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JamSeshun.Services;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// DSP chord tracker: capture → resample to 16 kHz → 512 ms chroma window every 128 ms
/// → template match against the song's chord vocabulary → advance-only line matching.
/// See <see cref="IChordTrackerService"/> for the contract and
/// <c>docs/PerformanceAudioTracking.md</c> for the design.
///
/// <para>
/// All analysis runs on a dedicated <see cref="EventLoopScheduler"/> (never the capture
/// callback or UI thread).  Per-hop cost is one 8192-point FFT plus a handful of
/// 12-element dot products — microseconds, so the effective latency is the analysis
/// window itself (~0.5 s) rather than compute.
/// </para>
/// </summary>
public sealed class ChordTrackerService : IChordTrackerService
{
    private const int TargetSampleRate = 16000;
    private const int WindowSize = 8192;              // 512 ms at 16 kHz
    private const int HopSize = 2048;                 // 128 ms at 16 kHz
    private const float ConfidenceThreshold = 0.6f;   // advance gate (docs — Strategy A)
    private const int HoldHops = 2;                   // consecutive matching hops required
    private const float MinRms = 0.003f;              // energy gate: skip near-silence

    private readonly IAudioCaptureService captureService;
    private readonly Func<IScheduler> schedulerFactory;

    public ChordTrackerService(IAudioCaptureService captureService)
        : this(captureService, () => new EventLoopScheduler())
    {
    }

    /// <summary>Test seam: inject <see cref="Scheduler.Immediate"/> for synchronous analysis.</summary>
    public ChordTrackerService(IAudioCaptureService captureService, Func<IScheduler> schedulerFactory)
    {
        this.captureService = captureService;
        this.schedulerFactory = schedulerFactory;
    }

    public IObservable<ChordTrackingUpdate> TrackChords(
        AudioCaptureDevice device,
        IReadOnlyList<PerformanceLine> lines,
        IObservable<int> position,
        int capo = 0)
    {
        return Observable.Create<ChordTrackingUpdate>(observer =>
        {
            var matcher = new ChordTemplateMatcher(lines.SelectMany(l => l.Chords), capo);
            var session = new TrackingSession(lines, matcher, capo, observer.OnNext);
            var scheduler = this.schedulerFactory();

            var subscriptions = new CompositeDisposable(scheduler as IDisposable ?? Disposable.Empty);

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
        // Stateless between tracking runs; sessions are torn down via their subscriptions.
    }

    /// <summary>Per-subscription mutable state, only ever touched on the analysis scheduler.</summary>
    private sealed class TrackingSession
    {
        private readonly IReadOnlyList<PerformanceLine> lines;
        private readonly ChordTemplateMatcher matcher;
        private readonly int capo;
        private readonly Action<ChordTrackingUpdate> emit;
        private readonly ChromaExtractor chroma = new(TargetSampleRate);
        private readonly SlidingAudioWindow slidingWindow = new(TargetSampleRate, WindowSize, HopSize);
        private int currentIndex;
        private int holdCount;

        public TrackingSession(
            IReadOnlyList<PerformanceLine> lines,
            ChordTemplateMatcher matcher,
            int capo,
            Action<ChordTrackingUpdate> emit)
        {
            this.lines = lines;
            this.matcher = matcher;
            this.capo = capo;
            this.emit = emit;
        }

        public void SetPosition(int index)
        {
            if (index != this.currentIndex)
            {
                this.currentIndex = index;
                this.holdCount = 0;
            }
        }

        public void Process(AudioFrame frame) =>
            this.slidingWindow.Process(frame, this.Analyze);

        private void Analyze(float[] window)
        {
            int next = this.currentIndex + 1;
            if (next >= this.lines.Count)
            {
                return;
            }

            if (SlidingAudioWindow.Rms(window) < MinRms)
            {
                this.holdCount = 0;
                return;
            }

            var match = this.matcher.Classify(this.chroma.Extract(window));
            if (match == null || match.Confidence < ConfidenceThreshold)
            {
                this.holdCount = 0;
                return;
            }

            // Advance only when the detected chord belongs to the NEXT line but not the
            // current one — a chord shared by both lines is no evidence of progression.
            bool inNext = this.ContainsChord(this.lines[next], match.PitchClasses);
            bool inCurrent = this.currentIndex >= 0 &&
                             this.currentIndex < this.lines.Count &&
                             this.ContainsChord(this.lines[this.currentIndex], match.PitchClasses);

            if (inNext && !inCurrent)
            {
                this.holdCount++;
                if (this.holdCount >= HoldHops)
                {
                    this.holdCount = 0;
                    this.currentIndex = next; // advance locally too, ahead of position feedback
                    this.emit(new ChordTrackingUpdate(next, match.ChordName, match.Confidence));
                }
            }
            else
            {
                this.holdCount = 0;
            }
        }

        private bool ContainsChord(PerformanceLine line, IReadOnlySet<int> pitchClasses) =>
            line.Chords.Any(c => ChordNotes.GetPitchClasses(c, this.capo).SetEquals(pitchClasses));
    }
}
