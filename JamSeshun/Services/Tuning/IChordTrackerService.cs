namespace JamSeshun.Services.Tuning;

/// <summary>
/// On-device chord-recognition tracker.  Takes raw audio from the microphone, resamples
/// 44.1 kHz capture to 16 kHz, extracts a chromagram per hop, and template-matches it
/// against the chords of the song being performed (see <see cref="ChordTemplateMatcher"/>).
/// Emits the 0-based index of the <see cref="PerformanceLine"/> to advance to.
/// Only advances; never retreats.
///
/// <para>
/// The default implementation (<see cref="ChordTrackerService"/>) is a pure-DSP
/// chromagram matcher: no model download, per-hop cost far below the 128 ms hop
/// interval.  A learned classifier (e.g. DistilHuBERT ONNX, see
/// <c>docs/PerformanceAudioTracking.md</c> — Strategy A) can replace the matcher behind
/// this same interface later.
/// </para>
///
/// <para>
/// Advance logic: the detected chord is compared (by pitch-class set, with enharmonic
/// normalisation) against the chords of <c>lines[current + 1]</c>.  A match at
/// confidence ≥ 0.6 held for two consecutive hops triggers an advance — but only when
/// the detected chord is NOT also in the current line's chord set, otherwise songs
/// whose lines share a chord vocabulary (very common) would run away line-by-line.
/// Confidence is surfaced in <see cref="ChordTrackingUpdate"/> so the UI can dim the
/// indicator at low confidence.
/// </para>
///
/// <para>
/// <paramref name="position"/> (see <see cref="TrackChords"/>) carries the caller's
/// current line index, including manual Prev/Next navigation, so the tracker always
/// listens for the line after the one actually on screen.
/// </para>
///
/// <para>
/// Registered as an optional singleton.  <see cref="JamSeshun.ViewModels.PerformanceViewModel"/>
/// accepts a nullable instance; the tracking toggle is hidden from the UI when absent.
/// Disposing the returned subscription stops an individual tracking run without
/// affecting other callers.
/// </para>
/// </summary>
public interface IChordTrackerService : IDisposable
{
    /// <summary>
    /// Starts classifying chords from <paramref name="device"/> audio and emits a
    /// <see cref="ChordTrackingUpdate"/> each time the matched performance-line index
    /// should advance.  <paramref name="position"/> is the caller's current line index
    /// (emitting the initial value immediately, then on every navigation).  Emitted
    /// indices are always greater than the latest observed position.  Does not complete
    /// on its own; dispose the subscription to stop tracking.
    /// </summary>
    IObservable<ChordTrackingUpdate> TrackChords(
        AudioCaptureDevice device,
        IReadOnlyList<PerformanceLine> lines,
        IObservable<int> position);
}
