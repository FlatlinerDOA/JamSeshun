using System.Reactive.Linq;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

internal class NullTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
        Observable.Return<IReadOnlyList<AudioCaptureDevice>>(
            [new AudioCaptureDevice(0, "Headless (no audio)", IsDefault: true)]);

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device) =>
        Observable.Never<DetectedPitch>();
}
