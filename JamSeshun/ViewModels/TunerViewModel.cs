using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

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

    public TunerViewModel()
    {
        this.StartCommand = new RelayCommand(ToggleStartStop);
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
        if (IsRunning) Stop();
        else Start();
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
        if (tuningService == null || IsRunning) return;

        var device = SelectedDevice;
        if (device == null) return;

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
                if (f.EstimatedFrequency > 0) RawFrequency = f.EstimatedFrequency;
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
            this.CurrentErrorInCents = pitch.ErrorInCents;
            // Map ±50 cents to ±55° from vertical for the gauge needle.
            this.CurrentErrorInDegrees = (float)(Math.Max(-50, Math.Min(50, pitch.ErrorInCents)) / 50.0 * 55.0);
        }
        else
        {
            // No confident pitch — clear the readout and center the needle.
            this.CurrentNote = string.Empty;
            this.CurrentFrequency = 0;
            this.CurrentErrorInCents = 0;
            this.CurrentErrorInDegrees = 0;
        }
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

    public RelayCommand StartCommand { get; }
}
