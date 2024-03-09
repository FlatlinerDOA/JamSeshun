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
            this.CurrentErrorInDegrees = (360f + x.ErrorInCents) % 360f; // * (Math.PI ^ 2d * 45d);
        });
    }

    public float CurrentFrequency
    {
        get => this.currentFrequency;
        set => this.SetProperty(ref this.currentFrequency, value);
    }


    public float CurrentErrorInCents
    {
        get => this.currentErrorInCents;
        set => this.SetProperty(ref this.currentErrorInCents, value);
    }

    public float CurrentErrorInDegrees
    {
        get => this.currentErrorInDegrees;
        set => this.SetProperty(ref this.currentErrorInDegrees, value);
    }

    public string CurrentNote
    {
        get { return currentNote; }
        set { this.SetProperty(ref currentNote, value); }
    }

    public RelayCommand StartCommand { get; }
}
