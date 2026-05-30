using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

public sealed class TunerViewModel : ViewModelBase
{
    private readonly ITuningService tuningService;
    private float currentFrequency;
    private float currentErrorInCents;
    private float currentErrorInDegrees;
    private string currentNote;

    public TunerViewModel()
    {
        this.StartCommand = new RelayCommand(() => this.Start());
    }

    public TunerViewModel(ITuningService tuningService) : this()
    {
        this.tuningService = tuningService;
    }

    private IDisposable recordingSession;
    private void Start()
    {
        if (this.recordingSession != null)
        {
            this.recordingSession.Dispose();
            this.recordingSession = null;
            IsRunning = false;
            return;
        }

        var q = from audioDeviceList in this.tuningService.GetAudioCaptureDevices()
                let audioDevice = audioDeviceList.FirstOrDefault(f => f.IsDefault) ?? audioDeviceList.FirstOrDefault(f => f.Name.Contains("Yeti"))
                from detectedPitches in this.tuningService.StartDetectingPitch(audioDevice).Buffer(TimeSpan.FromMilliseconds(300))
                let n = detectedPitches.GroupBy(p => p.Fundamental).OrderByDescending(g => g.Count()).FirstOrDefault()
                where n != null
                let avgFreq = n.Average(d => d.EstimatedFrequency)
                select new DetectedPitch(avgFreq, n.Key, n.Key.GetCentsError(avgFreq));
        
        this.recordingSession = q.ObserveOn(AvaloniaScheduler.Instance).Subscribe(x =>
        {
            this.CurrentNote = x.Fundamental.ToString();
            this.CurrentFrequency = x.EstimatedFrequency;
            this.CurrentErrorInCents = x.ErrorInCents;
            // Map ±50 cents to ±55° from vertical for the gauge needle
            this.CurrentErrorInDegrees = (float)(Math.Max(-50, Math.Min(50, x.ErrorInCents)) / 50.0 * 55.0);
        });
        IsRunning = true;
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
        set { SetProperty(ref currentNote, value); OnPropertyChanged(nameof(NoteDisplay)); }
    }

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

    public RelayCommand StartCommand { get; }
}
