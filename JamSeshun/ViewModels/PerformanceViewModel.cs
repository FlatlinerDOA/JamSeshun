using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

/// <summary>
/// Drives the performance view: steps through <see cref="PerformanceLine"/> records
/// (chord line + lyric + section label) parsed from a saved tab.
///
/// Audio-based auto-advance is provided by <see cref="IChordTrackerService"/> and
/// <see cref="ILyricTrackerService"/> (both optional; hidden when null).
/// See <c>docs/PerformanceAudioTracking.md</c> for the full design.
/// </summary>
public class PerformanceViewModel : ViewModelBase, IOnShow, IDisposable
{
    private readonly INavigationService? navigation;
    private readonly IChordTrackerService? chordTracker;
    private readonly ILyricTrackerService? lyricTracker;
    private IReadOnlyList<PerformanceLine> performanceLines = [];
    private int currentIndex;
    private string song = string.Empty;
    private string artist = string.Empty;
    private string tuningDisplay = string.Empty;
    private AudioCaptureDevice? trackingDevice = null; // populated when tracking device selection is wired
    private bool audioTrackingEnabled;

    public PerformanceViewModel()
    {
        this.BackCommand     = new AsyncRelayCommand(this.OnBack);
        this.NextCommand     = new RelayCommand(this.GoNext, () => this.CanGoNext);
        this.PreviousCommand = new RelayCommand(this.GoPrevious, () => this.CanGoPrevious);
    }

    public PerformanceViewModel(
        INavigationService navigation,
        IChordTrackerService? chordTracker = null,
        ILyricTrackerService? lyricTracker = null) : this()
    {
        this.navigation = navigation;
        this.chordTracker = chordTracker;
        this.lyricTracker = lyricTracker;
    }

    // ── IOnShow ──────────────────────────────────────────────────────────────

    public IDisposable OnShow()
    {
        var subscriptions = new CompositeDisposable();

        if (this.chordTracker != null && this.audioTrackingEnabled && this.trackingDevice != null)
        {
            subscriptions.Add(
                this.chordTracker
                    .TrackChords(this.trackingDevice, this.performanceLines)
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .Subscribe(u => this.AdvanceTo(u.LineIndex)));
        }

        if (this.lyricTracker != null && this.audioTrackingEnabled && this.trackingDevice != null)
        {
            var lyrics = this.performanceLines.Select(l => l.LyricText).ToList();
            subscriptions.Add(
                this.lyricTracker
                    .TrackLyrics(this.trackingDevice, lyrics)
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .Subscribe(u => this.AdvanceTo(u.LineIndex)));
        }

        return subscriptions;
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    /// <summary>Called by <see cref="TabViewModel"/> during navigation.</summary>
    public void LoadForTab(SavedTab tab)
    {
        this.song = tab.Song;
        this.artist = tab.Artist;

        var parts = new List<string>(2);
        if (!string.IsNullOrEmpty(tab.Tuning))
        {
            parts.Add(tab.Tuning);
        }

        if (tab.Capo > 0)
        {
            parts.Add($"Capo {tab.Capo}");
        }

        this.tuningDisplay = string.Join("  ·  ", parts);
        this.performanceLines = WikiTabParser.ParsePerformanceLines(tab.Content);
        this.currentIndex = 0;
        this.NotifyAll();
    }

    // ── Song metadata ────────────────────────────────────────────────────────

    public string Song => this.song;
    public string Artist => this.artist;
    public string TuningDisplay => this.tuningDisplay;
    public bool HasTuning => !string.IsNullOrEmpty(this.tuningDisplay);

    // ── Line state ───────────────────────────────────────────────────────────

    public bool HasLines => this.performanceLines.Count > 0;
    public int TotalLines => this.performanceLines.Count;
    public int Position => this.HasLines ? this.currentIndex + 1 : 0;
    public string ProgressText => this.HasLines ? $"{this.Position} / {this.TotalLines}" : string.Empty;

    // Current line — exposed as flat properties so compiled XAML bindings
    // never need to null-traverse through a nullable record.
    public PerformanceLine? CurrentLine =>
        this.HasLines ? this.performanceLines[this.currentIndex] : null;

    public IReadOnlyList<Chord> CurrentLineChords =>
        this.CurrentLine?.Chords ?? [];

    public string CurrentLineLyric =>
        this.CurrentLine?.LyricText ?? string.Empty;

    public bool CurrentLineHasLyric =>
        !string.IsNullOrEmpty(this.CurrentLine?.LyricText);

    public string CurrentLineSectionLabel =>
        this.CurrentLine?.SectionLabel ?? string.Empty;

    public bool CurrentLineHasSectionLabel =>
        !string.IsNullOrEmpty(this.CurrentLine?.SectionLabel);

    // Next three lines shown in the preview strip
    public IReadOnlyList<PerformanceLine> UpcomingLines =>
        this.performanceLines.Skip(this.currentIndex + 1).Take(3).ToList();

    public bool CanGoNext     => this.currentIndex < this.performanceLines.Count - 1;
    public bool CanGoPrevious => this.currentIndex > 0;

    // ── Audio tracking ───────────────────────────────────────────────────────

    public bool CanAudioTrack => this.chordTracker != null || this.lyricTracker != null;

    public bool AudioTrackingEnabled
    {
        get => this.audioTrackingEnabled;
        set => this.SetProperty(ref this.audioTrackingEnabled, value);
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public AsyncRelayCommand BackCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand PreviousCommand { get; }

    // ── Private ──────────────────────────────────────────────────────────────

    private void AdvanceTo(int lineIndex)
    {
        if (lineIndex > this.currentIndex && lineIndex < this.performanceLines.Count)
        {
            this.currentIndex = lineIndex;
            this.NotifyNavigationChanged();
        }
    }

    private void GoNext()
    {
        if (this.currentIndex < this.performanceLines.Count - 1)
        {
            this.currentIndex++;
            this.NotifyNavigationChanged();
        }
    }

    private void GoPrevious()
    {
        if (this.currentIndex > 0)
        {
            this.currentIndex--;
            this.NotifyNavigationChanged();
        }
    }

    private void NotifyNavigationChanged()
    {
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLine));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLineChords));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLineLyric));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLineHasLyric));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLineSectionLabel));
        this.OnPropertyChanged(nameof(PerformanceViewModel.CurrentLineHasSectionLabel));
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
        // Subscriptions from OnShow() are owned by the view via IOnShow contract.
    }
}
