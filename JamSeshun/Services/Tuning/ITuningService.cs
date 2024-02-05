namespace JamSeshun.Services.Tuning;

public interface ITuningService
{
    IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices();

    IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device);
}
