namespace JamSeshun.Services.Tuning;

/// <summary>
/// On-device ASR-based lyric tracker.  Listens to microphone audio and emits the
/// index of the lyric line (within the supplied list) that best matches the current
/// recognition window, advancing only forward.
///
/// <para>
/// The default implementation (<see cref="AsrLyricTrackerService"/>) owns the audio
/// pipeline — capture, 44.1 kHz → 16 kHz resampling, energy VAD gate, 5-second rolling
/// window with 1-second stride — and delegates recognition to an
/// <see cref="ISpeechTranscriber"/> (Whisper tiny ONNX being the intended engine; see
/// <c>docs/PerformanceAudioTracking.md</c> for model selection and platform notes).
/// Matching against lyric lines is done by <see cref="LyricMatcher"/> (token overlap
/// with hysteresis).
/// </para>
///
/// <para>
/// <paramref name="position"/> (see <see cref="TrackLyrics"/>) carries the caller's
/// current line index, including manual Prev/Next navigation, so matching always
/// starts from the line actually on screen.  Lines with empty lyric text
/// (instrument-only passages) are skipped by the matcher; the chord tracker handles
/// progression through those.
/// </para>
///
/// <para>
/// Registered as an optional singleton.  <see cref="JamSeshun.ViewModels.PerformanceViewModel"/>
/// accepts a nullable instance; when absent the lyric-tracking path is simply not
/// subscribed.  Disposing the service tears down the recognition engine; disposing the
/// returned subscription stops an individual tracking run without affecting other callers.
/// </para>
/// </summary>
public interface ILyricTrackerService : IDisposable
{
    /// <summary>
    /// Starts recognising audio from <paramref name="device"/> and emits a
    /// <see cref="LyricTrackingUpdate"/> whose index is the 0-based lyric line within
    /// <paramref name="lines"/> to advance to.  <paramref name="position"/> is the
    /// caller's current line index (emitting the initial value immediately, then on
    /// every navigation).  Emitted indices are always greater than the latest observed
    /// position.  Does not complete on its own; dispose the subscription to stop tracking.
    /// </summary>
    IObservable<LyricTrackingUpdate> TrackLyrics(
        AudioCaptureDevice device,
        IReadOnlyList<string> lines,
        IObservable<int> position);
}
