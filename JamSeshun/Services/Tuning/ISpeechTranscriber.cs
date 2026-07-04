namespace JamSeshun.Services.Tuning;

/// <summary>
/// Speech-to-text engine seam used by <see cref="AsrLyricTrackerService"/>.  The
/// intended production implementation is Whisper tiny (INT8 ONNX, download-on-demand
/// — see <c>docs/PerformanceAudioTracking.md</c> — Strategy B); tests use a scripted
/// fake.  Keeping the engine behind this seam means the whole tracking pipeline
/// (capture, resample, VAD, windowing, matching) is testable without a model.
/// </summary>
public interface ISpeechTranscriber : IDisposable
{
    /// <summary>
    /// Transcribes a window of mono PCM float32 audio.  Called off the UI thread with
    /// at most one transcription in flight; implementations may still be slow (hundreds
    /// of ms) — the caller drops windows rather than queueing behind a slow engine.
    /// Returns an empty string when nothing intelligible was heard.
    /// </summary>
    Task<string> TranscribeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken);
}
