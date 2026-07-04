using System.Reactive;
using System.Reactive.Linq;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

/// <summary>
/// Drives the performance view: steps through <see cref="PerformanceLine"/> records
/// (chord line + lyric + section label) parsed from a saved tab.
///
/// Audio-based auto-advance is provided by <see cref="IChordTrackerService"/> and
/// <see cref="ILyricTrackerService"/> (both optional; the toggle is hidden when
/// neither is registered).  The trackers observe <see cref="position"/> so manual
/// Prev/Next keeps them in sync, and their advance-only updates flow back through
/// <see cref="AdvanceTo"/>.  See <c>docs/PerformanceAudioTracking.md</c>.
/// </summary>
public class PerformanceViewModel : ViewModelBase, IOnShow, IDisposable
{
    private readonly INavigationService? navigation;
    private readonly IChordTrackerService? chordTracker;
    private readonly ILyricTrackerService? lyricTracker;
    private readonly IAudioCaptureService? captureService;
    private readonly BehaviorSubject<int> position = new(0);
    private IReadOnlyList<PerformanceLine> performanceLines = [];
    private int currentIndex;
    private AudioCaptureDevice? guitarDevice;
    private AudioCaptureDevice? voiceDevice;
    private bool audioTrackingEnabled;
    private string lastDetectedChord = string.Empty;
    private float trackingConfidence;

    // Non-null only while the view is shown; holds the live tracker subscriptions.
    private SerialDisposable? trackingSlot;

    public PerformanceViewModel()
    {
        this.BackCommand = new AsyncRelayCommand(this.OnBack);
        this.NextCommand = new RelayCommand(this.GoNext, () => this.CanGoNext);
        this.PreviousCommand = new RelayCommand(this.GoPrevious, () => this.CanGoPrevious);
    }

    public PerformanceViewModel(
        INavigationService navigation,
        IChordTrackerService? chordTracker = null,
        ILyricTrackerService? lyricTracker = null,
        IAudioCaptureService? captureService = null) : this()
    {
        this.navigation = navigation;
        this.chordTracker = chordTracker;
        this.lyricTracker = lyricTracker;
        this.captureService = captureService;
    }

    // ── IOnShow ──────────────────────────────────────────────────────────────

    public IDisposable OnShow()
    {
        var trackingSubscription = new SerialDisposable();
        this.trackingSlot = trackingSubscription;

        var subscriptions = new CompositeDisposable(
            trackingSubscription,
            Disposable.Create(() => this.trackingSlot = null));

        if (this.captureService != null && this.CanAudioTrack)
        {
            subscriptions.Add(this.captureService
                .GetAudioCaptureDevices()
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(devices =>
                {
                    this.Devices.Clear();
                    foreach (var d in devices)
                    {
                        this.Devices.Add(d);
                    }

                    var defaultDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
                    this.guitarDevice ??= defaultDevice;
                    this.voiceDevice ??= defaultDevice;
                    this.OnPropertyChanged(nameof(PerformanceViewModel.GuitarDevice));
                    this.OnPropertyChanged(nameof(PerformanceViewModel.VoiceDevice));
                    this.UpdateTracking();
                }));
        }

        return subscriptions;
    }

    /// <summary>Capture devices discovered on show; source for the Guitar/Voice pickers.</summary>
    public ObservableCollection<AudioCaptureDevice> Devices { get; } = new();

    // ── Data loading ─────────────────────────────────────────────────────────

    /// <summary>Called by <see cref="TabViewModel"/> during navigation.</summary>
    public void LoadForTab(SavedTab tab)
    {
        this.Song = tab.Song;
        this.Artist = tab.Artist;

        var parts = new List<string>(2);
        if (!string.IsNullOrEmpty(tab.Tuning))
        {
            parts.Add(tab.Tuning);
        }

        if (tab.Capo > 0)
        {
            parts.Add($"Capo {tab.Capo}");
        }

        this.TuningDisplay = string.Join("  ·  ", parts);
        this.performanceLines = WikiTabParser.ParsePerformanceLines(tab.Content);
        this.currentIndex = 0;
        this.position.OnNext(0);
        this.NotifyAll();

        // Trackers hold the line list from when they started — restart on new content.
        this.UpdateTracking();
    }

    // ── Song metadata ────────────────────────────────────────────────────────

    public string Song { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public string TuningDisplay { get; private set; } = string.Empty;
    public bool HasTuning => !string.IsNullOrEmpty(this.TuningDisplay);

    // ── Line state ───────────────────────────────────────────────────────────

    public bool HasLines => this.performanceLines.Count > 0;
    public int TotalLines => this.performanceLines.Count;
    public int Position => this.HasLines ? this.currentIndex + 1 : 0;
    public string ProgressText => this.HasLines ? $"{this.Position} / {this.TotalLines}" : string.Empty;

    public PerformanceLineViewModel PreviousLine { get; private set; } = new(null);

    /// <summary>
    /// Gets the Current line — exposed as flat properties so compiled XAML bindings
    /// never need to null-traverse through a nullable record.
    /// </summary>
    public PerformanceLineViewModel CurrentLine { get; private set; } = new(null);

    /// <summary>
    /// Gets the next line after the one that's been detected (current)
    /// </summary>
    public PerformanceLineViewModel? NextLine { get; private set; } = new(null);

    // Next three lines shown in the preview strip
    public IReadOnlyList<PerformanceLine> UpcomingLines =>
        this.performanceLines.Skip(this.currentIndex + 3).Take(1).ToList();

    public bool CanGoNext => this.currentIndex < this.performanceLines.Count - 1;

    public bool CanGoPrevious => this.currentIndex > 0;

    // ── Audio tracking ───────────────────────────────────────────────────────

    public bool CanAudioTrack => this.chordTracker != null || this.lyricTracker != null;

    public bool AudioTrackingEnabled
    {
        get => this.audioTrackingEnabled;
        set
        {
            if (this.SetProperty(ref this.audioTrackingEnabled, value))
            {
                this.UpdateTracking();
                this.OnPropertyChanged(nameof(PerformanceViewModel.TrackingStatusText));
                this.OnPropertyChanged(nameof(PerformanceViewModel.IsTracking));
            }
        }
    }

    /// <summary>Capture device used by the chord tracker. Changing it while tracking restarts.</summary>
    public AudioCaptureDevice? GuitarDevice
    {
        get => this.guitarDevice;
        set
        {
            if (this.SetProperty(ref this.guitarDevice, value))
            {
                this.UpdateTracking();
            }
        }
    }

    /// <summary>Capture device used by the lyric tracker. Changing it while tracking restarts.</summary>
    public AudioCaptureDevice? VoiceDevice
    {
        get => this.voiceDevice;
        set
        {
            if (this.SetProperty(ref this.voiceDevice, value))
            {
                this.UpdateTracking();
            }
        }
    }

    /// <summary>True while at least one tracker subscription is live (enabled, shown, a device found).</summary>
    public bool IsTracking =>
        this.audioTrackingEnabled && this.trackingSlot != null &&
        (this.guitarDevice != null || this.voiceDevice != null);

    /// <summary>Chord label from the most recent auto-advance, for the tracking indicator.</summary>
    public string LastDetectedChord => this.lastDetectedChord;

    /// <summary>Confidence of the most recent tracker update (0–1); dims the indicator.</summary>
    public float TrackingConfidence => this.trackingConfidence;

    /// <summary>Indicator opacity: same fade pattern as the tuner's DisplayOpacity.</summary>
    public double TrackingIndicatorOpacity => 0.35 + 0.65 * Math.Clamp(this.trackingConfidence, 0f, 1f);

    public string TrackingStatusText =>
        !this.audioTrackingEnabled ? string.Empty
        : string.IsNullOrEmpty(this.lastDetectedChord) ? "listening…"
        : this.lastDetectedChord;

    // ── Commands ─────────────────────────────────────────────────────────────

    public AsyncRelayCommand BackCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand PreviousCommand { get; }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>(Re)starts or stops tracker subscriptions to match the current state.</summary>
    private void UpdateTracking()
    {
        if (this.trackingSlot == null)
        {
            return; // not shown
        }

        bool hasAnyDevice = this.guitarDevice != null || this.voiceDevice != null;
        if (!this.audioTrackingEnabled || !hasAnyDevice || !this.HasLines)
        {
            this.trackingSlot.Disposable = Disposable.Empty;
            this.OnPropertyChanged(nameof(PerformanceViewModel.IsTracking));
            return;
        }

        var lines = this.performanceLines;
        var subscriptions = new CompositeDisposable();

        if (this.chordTracker != null && this.guitarDevice != null)
        {
            subscriptions.Add(this.chordTracker
                .TrackChords(this.guitarDevice, lines, this.position)
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(this.OnChordTrackingUpdate));
        }

        if (this.lyricTracker != null && this.voiceDevice != null)
        {
            var lyrics = lines.Select(l => l.LyricText).ToList();
            subscriptions.Add(this.lyricTracker
                .TrackLyrics(this.voiceDevice, lyrics, this.position)
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(this.OnLyricTrackingUpdate));
        }

        this.trackingSlot.Disposable = subscriptions;
        this.OnPropertyChanged(nameof(PerformanceViewModel.IsTracking));
    }

    private void OnChordTrackingUpdate(ChordTrackingUpdate update)
    {
        this.lastDetectedChord = update.DetectedChord;
        this.trackingConfidence = update.Confidence;
        this.NotifyTrackingChanged();
        this.AdvanceTo(update.LineIndex);
    }

    private void OnLyricTrackingUpdate(LyricTrackingUpdate update)
    {
        this.trackingConfidence = update.Confidence;
        this.NotifyTrackingChanged();
        this.AdvanceTo(update.LineIndex);
    }

    private void NotifyTrackingChanged()
    {
        this.OnPropertyChanged(nameof(PerformanceViewModel.LastDetectedChord));
        this.OnPropertyChanged(nameof(PerformanceViewModel.TrackingConfidence));
        this.OnPropertyChanged(nameof(PerformanceViewModel.TrackingIndicatorOpacity));
        this.OnPropertyChanged(nameof(PerformanceViewModel.TrackingStatusText));
    }

    private void AdvanceTo(int lineIndex)
    {
        if (lineIndex > this.currentIndex && lineIndex < this.performanceLines.Count)
        {
            this.currentIndex = lineIndex;
            this.position.OnNext(lineIndex);
            this.NotifyNavigationChanged();
        }
    }

    private void GoNext()
    {
        if (this.currentIndex < this.performanceLines.Count - 1)
        {
            this.currentIndex++;
            this.position.OnNext(this.currentIndex);
            this.NotifyNavigationChanged();
        }
    }

    private void GoPrevious()
    {
        if (this.currentIndex > 0)
        {
            this.currentIndex--;
            this.position.OnNext(this.currentIndex);
            this.NotifyNavigationChanged();
        }
    }

    private void NotifyNavigationChanged()
    {
        this.PreviousLine = this.HasLines ? new(this.performanceLines[this.currentIndex]) : new(null);
        this.CurrentLine = this.HasLines && this.currentIndex + 1 < this.performanceLines.Count ? new(this.performanceLines[this.currentIndex + 1]) : new(null);
        this.NextLine = this.HasLines && this.currentIndex + 2 < this.performanceLines.Count ? new(this.performanceLines[this.currentIndex + 2]) : new(null);

        this.OnPropertyChanged(nameof(PerformanceViewModel.PreviousLine));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLine));
        this.OnPropertyChanged(nameof(PerformanceViewModel.NextLine));
        this.OnPropertyChanged(nameof(PerformanceViewModel.UpcomingLines));
        this.OnPropertyChanged(nameof(PerformanceViewModel.Position));
        this.OnPropertyChanged(nameof(PerformanceViewModel.ProgressText));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CanGoNext));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CanGoPrevious));
        this.NextCommand.NotifyCanExecuteChanged();
        this.PreviousCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAll()
    {
        this.OnPropertyChanged(nameof(PerformanceViewModel.Song));
        this.OnPropertyChanged(nameof(PerformanceViewModel.Artist));
        this.OnPropertyChanged(nameof(PerformanceViewModel.TuningDisplay));
        this.OnPropertyChanged(nameof(PerformanceViewModel.HasTuning));
        this.OnPropertyChanged(nameof(PerformanceViewModel.HasLines));
        this.OnPropertyChanged(nameof(PerformanceViewModel.TotalLines));
        this.NotifyNavigationChanged();
    }

    private async Task OnBack()
    {
        if (this.navigation != null)
        {
            await this.navigation.PopAsync();
        }
    }

    public void Dispose()
    {
        // Subscriptions from OnShow() are owned by the view via the IOnShow contract;
        // the position subject is owned here.
        this.position.Dispose();
    }
}
