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
    private string currentNote;

    public TunerViewModel()
    {
        this.StartCommand = new RelayCommand(() => this.Start());

    }

    public TunerViewModel(ITuningService tuningService) : this()
    {
        this.tuningService = tuningService;
    }

    private void Start()
    {
        var q = from audioDeviceList in this.tuningService.GetAudioCaptureDevices()
                let audioDevice = audioDeviceList.FirstOrDefault(f => f.Name.Contains("Yeti"))
                from detectedPitch in this.tuningService.StartDetectingPitch(audioDevice)
                select detectedPitch;
        
        q.ObserveOn(AvaloniaScheduler.Instance).Subscribe(x =>
        {
            this.CurrentNote = x.ClosestNote.Name;
            this.CurrentFrequency = x.EstimatedFrequency;
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

    public string CurrentNote
    {
        get { return currentNote; }
        set { currentNote = value; }
    }

    public RelayCommand StartCommand { get; }
}
