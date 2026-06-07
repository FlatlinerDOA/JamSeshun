using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

public record TuningStringDisplay(int Index, string NoteName, bool IsActive, bool IsInTune, bool IsLocked);

public sealed class TunerViewModel : ViewModelBase, IDisposable
{
    private readonly ITuningService? tuningService;
    private float currentFrequency;
    private float currentErrorInCents;
    private float currentErrorInDegrees;
    private float signalLevel;
    private float rawFrequency;
    private float confidence;
    private string currentNote = string.Empty;
    private AudioCaptureDevice? selectedDevice;
    private GuitarTuning? targetTuning = GuitarTuning.Standard;
    private int targetStringIndex = -1;
    private float targetStringCents;
    private int lockedStringIndex = -1;

    public static IReadOnlyList<GuitarTuning> AvailableTunings { get; } =
    [
        GuitarTuning.Standard,
        GuitarTuning.DropD,
        GuitarTuning.Dadgad,
        GuitarTuning.OpenG,
        GuitarTuning.OpenD,
        GuitarTuning.EbStandard,
    ];

    public TunerViewModel()
    {
        this.StartCommand      = new RelayCommand(this.ToggleStartStop);
        this.ClearTuningCommand = new RelayCommand(() => this.TargetTuning = null);
    }

    public TunerViewModel(ITuningService tuningService) : this()
    {
        this.tuningService = tuningService;
    }

    public ObservableCollection<AudioCaptureDevice> Devices { get; } = new();

    public AudioCaptureDevice? SelectedDevice
    {
        get => this.selectedDevice;
        set
        {
            if (this.SetProperty(ref this.selectedDevice, value) && this.IsRunning)
            {
                // Restart capture on the newly chosen device.
                this.Stop();
                this.Start();
            }
        }
    }

    private IDisposable? devicesSubscription;

    public void Load()
    {
        this.devicesSubscription?.Dispose();
        this.devicesSubscription = this.tuningService?.GetAudioCaptureDevices()
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(devices =>
            {
                this.Devices.Clear();
                foreach (var d in devices)
                    this.Devices.Add(d);
                this.SelectedDevice ??= devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
            });
    }

    private IDisposable? recordingSession;

    private void ToggleStartStop()
    {
        if (this.IsRunning)
        {
            this.Stop();
        }
        else
        {
            this.Start();
        }
    }

    private void Stop()
    {
        this.recordingSession?.Dispose();
        this.recordingSession = null;
        this.SignalLevel = 0;
        this.Confidence = 0;
        this.IsRunning = false;
    }

    private void Start()
    {
        if (this.tuningService == null || this.IsRunning)
        {
            return;
        }

        var device = this.SelectedDevice;
        if (device == null)
        {
            return;
        }

        // Share one capture stream between the live diagnostics readout and the
        // (slower) stabilized note detection so only a single recorder is opened.
        var frames = this.tuningService.StartDetectingPitch(device).Publish().RefCount();
        var stabilizer = new PitchStabilizer();

        this.recordingSession = new CompositeDisposable
        {
            // Per-frame: live signal level + raw frequency (diagnostics).
            frames.ObserveOn(AvaloniaScheduler.Instance).Subscribe(f =>
            {
                this.SignalLevel = f.SignalLevel;
                if (f.EstimatedFrequency > 0)
                {
                    this.RawFrequency = f.EstimatedFrequency;
                }
            }),

            // Stabilized note: confidence-gated, hysteresis-smoothed over 300ms windows.
            frames
                .Buffer(TimeSpan.FromMilliseconds(300))
                .Select(window => stabilizer.Process(window))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(this.ApplyStabilizedPitch)
        };
        this.IsRunning = true;
    }

    private void ApplyStabilizedPitch(StabilizedPitch pitch)
    {
        this.Confidence = pitch.Confidence;
        if (pitch.HasPitch)
        {
            this.CurrentNote = pitch.Note.ToString();
            this.CurrentFrequency = pitch.Frequency;

            // Update the target string first so targetStringCents is fresh.
            this.UpdateTargetString(pitch.Frequency);

            // In guide mode use the target-string error for the needle; in chromatic
            // mode use the nearest-semitone error as before.
            float centsError = (this.targetTuning != null && this.targetStringIndex >= 0)
                ? this.targetStringCents
                : pitch.ErrorInCents;

            this.CurrentErrorInCents = centsError;
            // Map ±50 cents to ±55° from vertical for the gauge needle.
            this.CurrentErrorInDegrees = (float)(Math.Max(-50, Math.Min(50, centsError)) / 50.0 * 55.0);
        }
        else
        {
            // No confident pitch — clear the readout and center the needle.
            this.CurrentNote = string.Empty;
            this.CurrentFrequency = 0;
            this.CurrentErrorInCents = 0;
            this.CurrentErrorInDegrees = 0;
            this.SetTargetString(-1, 0f);
        }
    }

    private void UpdateTargetString(float freq)
    {
        if (this.targetTuning == null) {
            this.SetTargetString(-1, 0f); return; }

        if (this.lockedStringIndex >= 0)
        {
            // Locked: always measure error against the pinned string.
            var note = this.targetTuning.Strings[this.lockedStringIndex];
            float f = freq;
            while (f > note.Frequency * 1.5f) f /= 2f;
            while (f < note.Frequency / 1.5f) f *= 2f;
            this.SetTargetString(this.lockedStringIndex, (float)(1200.0 * Math.Log2(f / note.Frequency)));
            return;
        }

        var best = this.targetTuning.Strings
            .Select((note, i) =>
            {
                float f = freq;
                while (f > note.Frequency * 1.5f) f /= 2f;
                while (f < note.Frequency / 1.5f) f *= 2f;
                return (i, cents: (float)(1200.0 * Math.Log2(f / note.Frequency)));
            })
            .MinBy(x => Math.Abs(x.cents));

        this.SetTargetString(best.i, best.cents);
    }

    private void SetTargetString(int index, float cents)
    {
        if (this.targetStringIndex == index && Math.Abs(this.targetStringCents - cents) < 0.5f)
        {
            return;
        }
        this.targetStringIndex = index;
        this.targetStringCents = cents;
        this.OnPropertyChanged(nameof(TunerViewModel.TuningStrings));
    }

    public float CurrentFrequency
    {
        get => this.currentFrequency;
        set {
            this.SetProperty(ref this.currentFrequency, value);
            this.OnPropertyChanged(nameof(TunerViewModel.FrequencyDisplay)); }
    }

    public float CurrentErrorInCents
    {
        get => this.currentErrorInCents;
        set {
            this.SetProperty(ref this.currentErrorInCents, value);
            this.OnPropertyChanged(nameof(TunerViewModel.ErrorDisplay)); }
    }

    public float CurrentErrorInDegrees
    {
        get => this.currentErrorInDegrees;
        set => this.SetProperty(ref this.currentErrorInDegrees, value);
    }

    public string CurrentNote
    {
        get => this.currentNote;
        set {
            this.SetProperty(ref this.currentNote, value);
            this.OnPropertyChanged(nameof(TunerViewModel.NoteDisplay));
            this.OnPropertyChanged(nameof(TunerViewModel.HasReading)); }
    }

    /// <summary>True when a confident note is currently being shown.</summary>
    public bool HasReading => !string.IsNullOrEmpty(this.currentNote);

    public float SignalLevel
    {
        get => this.signalLevel;
        set {
            this.SetProperty(ref this.signalLevel, value);
            this.OnPropertyChanged(nameof(TunerViewModel.DiagnosticsDisplay)); }
    }

    public float RawFrequency
    {
        get => this.rawFrequency;
        set {
            this.SetProperty(ref this.rawFrequency, value);
            this.OnPropertyChanged(nameof(TunerViewModel.DiagnosticsDisplay)); }
    }

    public float Confidence
    {
        get => this.confidence;
        set {
            this.SetProperty(ref this.confidence, value);
            this.OnPropertyChanged(nameof(TunerViewModel.DisplayOpacity)); }
    }

    /// <summary>Note/needle opacity: fades toward dim (but stays visible) as confidence drops.</summary>
    public double DisplayOpacity => 0.35 + 0.65 * Math.Clamp(this.confidence, 0f, 1f);

    private bool isRunning;
    public bool IsRunning
    {
        get => this.isRunning;
        private set {
            this.SetProperty(ref this.isRunning, value);
            this.OnPropertyChanged(nameof(TunerViewModel.StartButtonText)); }
    }

    public string NoteDisplay => string.IsNullOrEmpty(this.currentNote) ? "–" : this.currentNote;
    public string FrequencyDisplay => this.currentFrequency > 0 ? $"{this.currentFrequency:F1} Hz" : "– Hz";
    public string ErrorDisplay => this.currentErrorInCents != 0 ? $"{this.currentErrorInCents:+0.0;-0.0;0} cents" : string.Empty;
    public string StartButtonText => this.isRunning ? "Stop" : "Start Tuning";
    public string DiagnosticsDisplay => $"level {this.signalLevel:F3}   ·   raw {(this.rawFrequency > 0 ? $"{this.rawFrequency:F0} Hz" : "—")}";

    public GuitarTuning? TargetTuning
    {
        get => this.targetTuning;
        set
        {
            if (this.SetProperty(ref this.targetTuning, value))
            {
                this.OnPropertyChanged(nameof(TunerViewModel.HasTargetTuning));
                this.OnPropertyChanged(nameof(TunerViewModel.TargetTuningName));
                this.OnPropertyChanged(nameof(TunerViewModel.TuningPickerPlaceholder));
                this.OnPropertyChanged(nameof(TunerViewModel.TuningStrings));
                // Sync the ComboBox selection: only highlight the item when the tuning
                // is one of the predefined entries; custom tunings (from tabs) leave the
                // ComboBox at null/placeholder without resetting TargetTuning.
                this.OnPropertyChanged(nameof(TunerViewModel.SelectedPredefinedTuning));
                this.lockedStringIndex = -1;
                this.SetTargetString(-1, 0f);
            }
        }
    }

    // Bound to the tuning ComboBox (two-way). Only covers predefined tunings.
    // Setting it to null does NOT clear TargetTuning — that requires ClearTuningCommand.
    public GuitarTuning? SelectedPredefinedTuning
    {
        get => TunerViewModel.AvailableTunings.Contains(this.targetTuning) ? this.targetTuning : null;
        set { if (value != null)
            {
                this.TargetTuning = value;
            }
        }
    }

    public bool HasTargetTuning => this.targetTuning != null;
    public string TargetTuningName => this.targetTuning?.Name ?? string.Empty;

    // Shown in the ComboBox when no predefined item is selected.
    // Displays the custom tuning name (e.g. "D G C G C D") rather than "Chromatic".
    public string TuningPickerPlaceholder => this.HasTargetTuning
        ? this.TargetTuningName
        : "Chromatic  (no guide)";

    public IReadOnlyList<TuningStringDisplay> TuningStrings
    {
        get
        {
            var tuning = this.targetTuning ?? GuitarTuning.Standard;
            return tuning.Strings.Select((note, i) =>
            {
                bool isLocked = i == this.lockedStringIndex;
                bool isActive = i == this.targetStringIndex;
                bool isInTune = isActive && Math.Abs(this.targetStringCents) <= 15f;
                return new TuningStringDisplay(i, note.Name, isActive, isInTune, isLocked);
            }).ToList();
        }
    }

    public void ToggleLockString(int index)
    {
        this.lockedStringIndex = this.lockedStringIndex == index ? -1 : index;
        // Immediately mark the locked string as active so the pill updates before the next audio frame.
        if (this.lockedStringIndex >= 0)
        {
            this.targetStringIndex = this.lockedStringIndex;
        }
        this.OnPropertyChanged(nameof(TunerViewModel.TuningStrings));
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand ClearTuningCommand { get; }

    public void Dispose()
    {
        this.devicesSubscription?.Dispose();
        this.recordingSession?.Dispose();
    }
}
