using System.Reactive.Linq;
using System.Reactive.Subjects;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Scripted <see cref="IAudioCaptureService"/>: tests push <see cref="AudioFrame"/>s
/// through <see cref="Frames"/> as if they came from a microphone.
/// </summary>
public sealed class FakeAudioCaptureService : IAudioCaptureService
{
    public static readonly AudioCaptureDevice Device = new(1, "Fake Microphone", IsDefault: true);

    public Subject<AudioFrame> Frames { get; } = new();

    public int ActiveCaptures { get; private set; }

    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
        Observable.Return<IReadOnlyList<AudioCaptureDevice>>([FakeAudioCaptureService.Device]);

    public IObservable<AudioFrame> Capture(AudioCaptureDevice device) =>
        Observable.Defer(() =>
        {
            this.ActiveCaptures++;
            return this.Frames.Finally(() => this.ActiveCaptures--);
        });

    /// <summary>Pushes samples through as 100 ms capture callbacks.</summary>
    public void Push(float[] samples, int sampleRate)
    {
        foreach (var frame in AudioSynthesizer.Frames(samples, sampleRate))
        {
            this.Frames.OnNext(frame);
        }
    }
}
