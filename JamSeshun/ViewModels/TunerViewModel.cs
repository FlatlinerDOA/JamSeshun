using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

public record TuningStringDisplay(int Index, string NoteName, bool IsActive, bool IsInTune, bool IsLocked);

public sealed class TunerViewModel : ViewModelBase
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
        this.StartCommand      = new RelayCommand(ToggleStartStop);
        this.ClearTuningCommand = new RelayCommand(() => TargetTuning = null);
    }

    public TunerViewModel(ITuningService tuningService) : this()
    {
        this.tuningService = tuningService;
        LoadDevices();
    }

    public ObservableCollection<AudioCaptureDevice> Devices { get; } = new();

    public AudioCaptureDevice? SelectedDevice
    {
        get => selectedDevice;
        set
        {
            if (SetProperty(ref selectedDevice, value) && IsRunning)
            {
                // Restart capture on the newly chosen device.
                Stop();
                Start();
            }
        }
    }

    private void LoadDevices()
    {
        tuningService?.GetAudioCaptureDevices()
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(devices =>
            {
                Devices.Clear();
                foreach (var d in devices)
                    Devices.Add(d);
                SelectedDevice ??= devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
            });
    }

    private IDisposable? recordingSession;

    private void ToggleStartStop()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    private void Stop()
    {
        recordingSession?.Dispose();
        recordingSession = null;
        SignalLevel = 0;
        Confidence = 0;
        IsRunning = false;
    }

    private void Start()
    {
        if (tuningService == null || IsRunning)
        {
            return;
        }

        var device = SelectedDevice;
        if (device == null)
        {
            return;
        }

        // Share one capture stream between the live diagnostics readout and the
        // (slower) stabilized note detection so only a single recorder is opened.
        var frames = tuningService.StartDetectingPitch(device).Publish().RefCount();
        var stabilizer = new PitchStabilizer();

        recordingSession = new CompositeDisposable
        {
            // Per-frame: live signal level + raw frequency (diagnostics).
            frames.ObserveOn(AvaloniaScheduler.Instance).Subscribe(f =>
            {
                SignalLevel = f.SignalLevel;
                if (f.EstimatedFrequency > 0)
                {
                    RawFrequency = f.EstimatedFrequency;
                }
            }),

            // Stabilized note: confidence-gated, hysteresis-smoothed over 300ms windows.
            frames
                .Buffer(TimeSpan.FromMilliseconds(300))
                .Select(window => stabilizer.Process(window))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(ApplyStabilizedPitch)
        };
        IsRunning = true;
    }

    private void ApplyStabilizedPitch(StabilizedPitch pitch)
    {
        Confidence = pitch.Confidence;
        if (pitch.HasPitch)
        {
            this.CurrentNote = pitch.Note.ToString();
            this.CurrentFrequency = pitch.Frequency;

            // Update the target string first so targetStringCents is fresh.
            UpdateTargetString(pitch.Frequency);

            // In guide mode use the target-string error for the needle; in chromatic
            // mode use the nearest-semitone error as before.
            float centsError = (targetTuning != null && targetStringIndex >= 0)
                ? targetStringCents
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
            SetTargetString(-1, 0f);
        }
    }

    private void UpdateTargetString(float freq)
    {
        if (targetTuning == null) { SetTargetString(-1, 0f); return; }

        if (lockedStringIndex >= 0)
        {
            // Locked: always measure error against the pinned string.
            var note = targetTuning.Strings[lockedStringIndex];
            float f = freq;
            while (f > note.Frequency * 1.5f) f /= 2f;
            while (f < note.Frequency / 1.5f) f *= 2f;
            SetTargetString(lockedStringIndex, (float)(1200.0 * Math.Log2(f / note.Frequency)));
            return;
        }

        var best = targetTuning.Strings
            .Select((note, i) =>
            {
                float f = freq;
                while (f > note.Frequency * 1.5f) f /= 2f;
                while (f < note.Frequency / 1.5f) f *= 2f;
                return (i, cents: (float)(1200.0 * Math.Log2(f / note.Frequency)));
            })
            .MinBy(x => Math.Abs(x.cents));

        SetTargetString(best.i, best.cents);
    }

    private void SetTargetString(int index, float cents)
    {
        if (targetStringIndex == index && Math.Abs(targetStringCents - cents) < 0.5f)
        {
            return;
        }
        targetStringIndex = index;
        targetStringCents = cents;
        OnPropertyChanged(nameof(TuningStrings));
    }

    public float CurrentFrequency
    {
        get => currentFrequency;
        set { SetProperty(ref currentFrequency, value); OnPropertyChanged(nameof(FrequencyDisplay)); }
    }

    public float CurrentErrorInCents
    {
        get => currentErrorInCents;
        set { SetProperty(ref currentErrorInCents, value); OnPropertyChanged(nameof(ErrorDisplay)); }
    }

    public float CurrentErrorInDegrees
    {
        get => currentErrorInDegrees;
        set => SetProperty(ref currentErrorInDegrees, value);
    }

    public string CurrentNote
    {
        get => currentNote;
        set { SetProperty(ref currentNote, value); OnPropertyChanged(nameof(NoteDisplay)); OnPropertyChanged(nameof(HasReading)); }
    }

    /// <summary>True when a confident note is currently being shown.</summary>
    public bool HasReading => !string.IsNullOrEmpty(currentNote);

    public float SignalLevel
    {
        get => signalLevel;
        set { SetProperty(ref signalLevel, value); OnPropertyChanged(nameof(DiagnosticsDisplay)); }
    }

    public float RawFrequency
    {
        get => rawFrequency;
        set { SetProperty(ref rawFrequency, value); OnPropertyChanged(nameof(DiagnosticsDisplay)); }
    }

    public float Confidence
    {
        get => confidence;
        set { SetProperty(ref confidence, value); OnPropertyChanged(nameof(DisplayOpacity)); }
    }

    /// <summary>Note/needle opacity: fades toward dim (but stays visible) as confidence drops.</summary>
    public double DisplayOpacity => 0.35 + 0.65 * Math.Clamp(confidence, 0f, 1f);

    private bool isRunning;
    public bool IsRunning
    {
        get => isRunning;
        private set { SetProperty(ref isRunning, value); OnPropertyChanged(nameof(StartButtonText)); }
    }

    public string NoteDisplay => string.IsNullOrEmpty(currentNote) ? "–" : currentNote;
    public string FrequencyDisplay => currentFrequency > 0 ? $"{currentFrequency:F1} Hz" : "– Hz";
    public string ErrorDisplay => currentErrorInCents != 0 ? $"{currentErrorInCents:+0.0;-0.0;0} cents" : string.Empty;
    public string StartButtonText => isRunning ? "Stop" : "Start Tuning";
    public string DiagnosticsDisplay => $"level {signalLevel:F3}   ·   raw {(rawFrequency > 0 ? $"{rawFrequency:F0} Hz" : "—")}";

    public GuitarTuning? TargetTuning
    {
        get => targetTuning;
        set
        {
            if (SetProperty(ref targetTuning, value))
            {
                OnPropertyChanged(nameof(HasTargetTuning));
                OnPropertyChanged(nameof(TargetTuningName));
                OnPropertyChanged(nameof(TuningPickerPlaceholder));
                OnPropertyChanged(nameof(TuningStrings));
                // Sync the ComboBox selection: only highlight the item when the tuning
                // is one of the predefined entries; custom tunings (from tabs) leave the
                // ComboBox at null/placeholder without resetting TargetTuning.
                OnPropertyChanged(nameof(SelectedPredefinedTuning));
                lockedStringIndex = -1;
                SetTargetString(-1, 0f);
            }
        }
    }

    // Bound to the tuning ComboBox (two-way). Only covers predefined tunings.
    // Setting it to null does NOT clear TargetTuning — that requires ClearTuningCommand.
    public GuitarTuning? SelectedPredefinedTuning
    {
        get => AvailableTunings.Contains(targetTuning) ? targetTuning : null;
        set { if (value != null)
            {
                TargetTuning = value;
            }
        }
    }

    public bool HasTargetTuning => targetTuning != null;
    public string TargetTuningName => targetTuning?.Name ?? string.Empty;

    // Shown in the ComboBox when no predefined item is selected.
    // Displays the custom tuning name (e.g. "D G C G C D") rather than "Chromatic".
    public string TuningPickerPlaceholder => HasTargetTuning
        ? TargetTuningName
        : "Chromatic  (no guide)";

    public IReadOnlyList<TuningStringDisplay> TuningStrings
    {
        get
        {
            var tuning = targetTuning ?? GuitarTuning.Standard;
            return tuning.Strings.Select((note, i) =>
            {
                bool isLocked = i == lockedStringIndex;
                bool isActive = i == targetStringIndex;
                bool isInTune = isActive && Math.Abs(targetStringCents) <= 15f;
                return new TuningStringDisplay(i, note.Name, isActive, isInTune, isLocked);
            }).ToList();
        }
    }

    public void ToggleLockString(int index)
    {
        lockedStringIndex = lockedStringIndex == index ? -1 : index;
        // Immediately mark the locked string as active so the pill updates before the next audio frame.
        if (lockedStringIndex >= 0)
        {
            targetStringIndex = lockedStringIndex;
        }
        OnPropertyChanged(nameof(TuningStrings));
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand ClearTuningCommand { get; }
}
