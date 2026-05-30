namespace JamSeshun.Services;

/// <summary>
/// Frets array has 6 elements ordered E A D G B e:
///   -1 = muted (X), 0 = open (O), 1+ = fret number.
/// Null means no diagram data is available.
/// </summary>
public record Chord(string Name, int[]? Frets = null);
