namespace JamSeshun.Services.Tuning;

/// <summary>
/// On-device chord-recognition tracker using
/// <c>alejogil35/distilhubert-finetuned-chorddetection</c> (DistilHuBERT fine-tuned
/// for chord classification, estimated ~25–50 MB INT8 ONNX — must be verified on
/// export; latency likewise must be measured per device).
///
/// <para>
/// Takes raw audio from the microphone.  Audio captured at 44.1 kHz (the tuner
/// pipeline sample rate) must be resampled to 16 kHz (HuBERT input rate) before
/// inference.  Emits the 0-based index of the <see cref="PerformanceLine"/> whose
/// chord set matches what is currently being played.  Only advances; never retreats.
/// </para>
///
/// <para>
/// Advance logic: the detected chord label is compared (with enharmonic normalisation)
/// against the chord names of <c>lines[currentIndex + 1].Chords</c>.  A match at
/// confidence ≥ 0.6 held for two consecutive 1-second frames triggers an advance.
/// Confidence is surfaced in <see cref="ChordTrackingUpdate"/> so the UI can dim
/// the indicator at low confidence.
/// </para>
///
/// <para>
/// See <c>docs/PerformanceAudioTracking.md</c> — Strategy A for the full pipeline,
/// ONNX export steps, and execution-provider notes.  Note: execution providers
/// (CoreML, NNAPI, DirectML) require separate NuGet packages and explicit
/// <c>AppendExecutionProvider_*</c> calls; they are not selected automatically.
/// </para>
///
/// <para>
/// Registered as an optional singleton.  <see cref="JamSeshun.ViewModels.PerformanceViewModel"/>
/// accepts a nullable instance; the chord-tracking toggle is hidden from the UI when absent.
/// Disposing the service tears down the ONNX session; disposing the returned
/// subscription stops an individual tracking run without affecting other callers.
/// </para>
/// </summary>
public interface IChordTrackerService : IDisposable
{
    /// <summary>
    /// Starts classifying chords from <paramref name="device"/> audio and emits a
    /// <see cref="ChordTrackingUpdate"/> each time the matched performance-line index
    /// advances.  Only ever advances — never emits an index less than or equal to
    /// the last emitted value.  Does not complete on its own; dispose the subscription
    /// to stop tracking.
    /// </summary>
    IObservable<ChordTrackingUpdate> TrackChords(AudioCaptureDevice device, IReadOnlyList<PerformanceLine> lines);
}
