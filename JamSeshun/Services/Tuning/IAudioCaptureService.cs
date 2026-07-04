namespace JamSeshun.Services.Tuning;

/// <summary>
/// Platform-specific raw microphone capture.  Unlike <see cref="ITuningService"/> (which
/// emits already-analysed <see cref="DetectedPitch"/> frames), this exposes the raw PCM
/// stream so higher-level analysers (chord tracking, ASR lyric tracking) can share one
/// capture pipeline.  See <c>docs/PerformanceAudioTracking.md</c>.
///
/// <para>
/// Implementations must make <see cref="Capture"/> shareable: multiple simultaneous
/// subscribers to the same device receive the same frames from a single opened device
/// (e.g. via <c>Publish().RefCount()</c>), because some platforms use exclusive-mode
/// capture.  The device is opened on first subscription and closed when the last
/// subscriber disposes.
/// </para>
/// </summary>
public interface IAudioCaptureService
{
    /// <summary>Enumerates available capture devices.</summary>
    IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices();

    /// <summary>
    /// Streams mono PCM float32 frames from <paramref name="device"/>.  Never completes
    /// on its own; dispose the subscription to stop capturing.  Frames are emitted on a
    /// capture/background thread — callers must marshal to the UI thread themselves.
    /// </summary>
    IObservable<AudioFrame> Capture(AudioCaptureDevice device);
}
