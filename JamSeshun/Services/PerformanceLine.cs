namespace JamSeshun.Services;

/// <summary>
/// One step in the performance view: a chord line from the tab paired with the lyric
/// line immediately below it and the section heading that introduced this passage
/// (e.g. "Verse 1", "Chorus").
///
/// <para>
/// Built by <see cref="WikiTabParser.ParsePerformanceLines"/> and consumed by
/// <see cref="JamSeshun.ViewModels.PerformanceViewModel"/> and
/// <see cref="JamSeshun.Services.Tuning.IChordTrackerService"/>.
/// </para>
/// </summary>
/// <param name="SectionLabel">
/// Section heading for the first line in each new section (e.g. "Verse 1"); empty
/// string for subsequent lines in the same section so the view only renders it once.
/// </param>
/// <param name="Chords">Ordered chords as they appear on the chord line.</param>
/// <param name="LyricText">The lyric line immediately below the chord line, or empty.</param>
public sealed record PerformanceLine(
    string SectionLabel,
    IReadOnlyList<Chord> Chords,
    string LyricText);
