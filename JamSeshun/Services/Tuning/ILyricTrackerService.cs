namespace JamSeshun.Services.Tuning;

/// <summary>
/// On-device ASR-based lyric tracker.  Listens to microphone audio and emits the
/// index of the lyric line (within the supplied list) that best matches the current
/// recognition window, advancing only forward.
///
/// <para>
/// Intended implementation: Whisper tiny (default bundle) or Qwen-Audio (optional
/// download for sung vocals), both quantised to INT8 ONNX and executed via
/// <c>Microsoft.ML.OnnxRuntime</c> with explicit execution-provider configuration
/// (CoreML on iOS, NNAPI on Android, DirectML on Windows — each requires its own
/// NuGet package and <c>AppendExecutionProvider_*</c> call; not automatic).
/// Audio is resampled from the capture rate (44.1 kHz) to 16 kHz before inference.
/// A Silero VAD gate discards silent frames before the ASR window runs.
/// </para>
///
/// <para>
/// See <c>docs/PerformanceAudioTracking.md</c> for the full design, model selection
/// rationale, lyric-matching algorithm, latency estimates (all unverified —
/// must be measured per device), and platform notes.
/// </para>
///
/// <para>
/// Registered as an optional singleton.  <see cref="JamSeshun.ViewModels.PerformanceViewModel"/>
/// accepts a nullable instance; when absent the lyric-tracking toggle is hidden.
/// Disposing the service tears down the ONNX session; disposing the returned
/// subscription stops an individual tracking run without affecting other callers.
/// </para>
/// </summary>
public interface ILyricTrackerService : IDisposable
{
    /// <summary>
    /// Starts recognising audio from <paramref name="device"/> and emits the 0-based
    /// index of the lyric line within <paramref name="lines"/> that best matches the
    /// current audio window.  Only ever advances — never emits an index less than or
    /// equal to the last emitted value.  Does not complete on its own; dispose the
    /// subscription to stop tracking.
    /// </summary>
    IObservable<LyricTrackingUpdate> TrackLyrics(AudioCaptureDevice device, IReadOnlyList<string> lines);
}
