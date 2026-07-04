using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for pitch-class extraction and enharmonic normalisation used by the chord
/// tracker's template matching and advance logic.
/// </summary>
public class ChordNotesSpecification
{
    // ── Enharmonic normalisation ─────────────────────────────────────────────

    [Theory]
    [InlineData("Db", "C#")]
    [InlineData("Ebm7", "D#m7")]
    [InlineData("Gb", "F#")]
    [InlineData("Ab", "G#")]
    [InlineData("Bb7", "A#7")]
    [InlineData("C", "C")]
    [InlineData("F#m", "F#m")]
    [InlineData("D/F#", "D/F#")]
    [InlineData("Am/Gb", "Am/F#")]
    public void Normalize_CanonicalisesFlatsToSharps(string input, string expected)
    {
        Assert.Equal(expected, ChordNotes.Normalize(input));
    }

    [Theory]
    [InlineData("Db", "C#", true)]
    [InlineData("Ebm", "D#m", true)]
    [InlineData("C", "C", true)]
    [InlineData("C", "Cm", false)]
    [InlineData("Db", "D", false)]
    public void AreEquivalent_MatchesEnharmonicSpellings(string a, string b, bool expected)
    {
        Assert.Equal(expected, ChordNotes.AreEquivalent(a, b));
    }

    // ── Root parsing ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("C", 0)]
    [InlineData("C#m7", 1)]
    [InlineData("Db", 1)]
    [InlineData("A", 9)]
    [InlineData("Bb", 10)]
    [InlineData("B", 11)]
    public void ParseRootPitchClass_ReadsRootAndAccidental(string name, int expected)
    {
        Assert.Equal(expected, ChordNotes.ParseRootPitchClass(name));
    }

    [Fact]
    public void ParseRootPitchClass_ReturnsNullForNonChordText()
    {
        Assert.Null(ChordNotes.ParseRootPitchClass("Hello"));
        Assert.Null(ChordNotes.ParseRootPitchClass(string.Empty));
    }

    // ── Pitch classes from fingering ─────────────────────────────────────────

    [Fact]
    public void GetPitchClasses_FromFrets_UsesTheActualVoicing()
    {
        // Am = x02210 → A C E
        var chord = new Chord("Am", ChordLibrary.Lookup("Am"));

        var classes = ChordNotes.GetPitchClasses(chord);

        Assert.Equal(new HashSet<int> { 9, 0, 4 }, classes);
    }

    [Fact]
    public void GetPitchClasses_FromFrets_SkipsMutedStrings()
    {
        // D = xx0232 → D A F#
        var chord = new Chord("D", ChordLibrary.Lookup("D"));

        var classes = ChordNotes.GetPitchClasses(chord);

        Assert.Equal(new HashSet<int> { 2, 9, 6 }, classes);
    }

    // ── Pitch classes from name (no fingering available) ─────────────────────

    [Theory]
    [InlineData("C", new[] { 0, 4, 7 })]
    [InlineData("Cm", new[] { 0, 3, 7 })]
    [InlineData("C7", new[] { 0, 4, 7, 10 })]
    [InlineData("Cmaj7", new[] { 0, 4, 7, 11 })]
    [InlineData("Cm7", new[] { 0, 3, 7, 10 })]
    [InlineData("Csus4", new[] { 0, 5, 7 })]
    [InlineData("Csus2", new[] { 0, 2, 7 })]
    [InlineData("Cdim", new[] { 0, 3, 6 })]
    [InlineData("Caug", new[] { 0, 4, 8 })]
    [InlineData("C5", new[] { 0, 7 })]
    [InlineData("Cadd9", new[] { 0, 2, 4, 7 })]
    [InlineData("C6", new[] { 0, 4, 7, 9 })]
    [InlineData("F#m", new[] { 6, 9, 1 })]
    [InlineData("Bbm", new[] { 10, 1, 5 })]
    [InlineData("G/B", new[] { 7, 11, 2 })]
    public void GetPitchClasses_FromName_DerivesQualityIntervals(string name, int[] expected)
    {
        var classes = ChordNotes.GetPitchClasses(new Chord(name));

        Assert.Equal(expected.ToHashSet(), classes);
    }

    [Fact]
    public void GetPitchClasses_UnparseableName_ReturnsEmpty()
    {
        Assert.Empty(ChordNotes.GetPitchClasses(new Chord("Chorus")));
    }
}
