namespace JamSeshun.Services.Tuning;

/// <summary>
/// Emitted by <see cref="IChordTrackerService"/> each time the recognised chord
/// matches the next performance line.
/// </summary>
/// <param name="LineIndex">0-based index of the newly matched <see cref="PerformanceLine"/>.</param>
/// <param name="DetectedChord">Canonical chord label as classified by the model (e.g. "Am", "G").</param>
/// <param name="Confidence">Model softmax confidence for the detected chord (0–1).</param>
public sealed record ChordTrackingUpdate(int LineIndex, string DetectedChord, float Confidence);

/// <summary>
/// Emitted by <see cref="ILyricTrackerService"/> each time the transcribed fragment
/// best matches the next lyric line.
/// </summary>
/// <param name="LineIndex">0-based index of the newly matched lyric line.</param>
/// <param name="Confidence">Token-overlap score of the match (0–1).</param>
public sealed record LyricTrackingUpdate(int LineIndex, float Confidence);
