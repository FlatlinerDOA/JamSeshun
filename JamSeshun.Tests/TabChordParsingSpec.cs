using JamSeshun.Services;
using JamSeshun.ViewModels;

namespace JamSeshun.Tests;

public class TabChordParsingSpec
{
    private static IReadOnlyList<string> ChordNames(string content)
    {
        var vm = new TabViewModel { Tab = new SavedTab("Artist", "Song", content) };
        return vm.Chords.Select(c => c.Name).ToList();
    }

    // ── Valid chords are detected ────────────────────────────────────────────

    [Theory]
    [InlineData("Am")]
    [InlineData("G")]
    [InlineData("C#m7")]
    [InlineData("Cadd9")]
    [InlineData("Dsus4")]
    [InlineData("Csus2")]
    [InlineData("Dmaj9/F#")]
    [InlineData("Cm7b5")]
    [InlineData("C7sus4")]
    [InlineData("F#5/D")]
    [InlineData("D5/G")]
    [InlineData("C#/D")]
    [InlineData("A5")]
    [InlineData("AbM7")]
    [InlineData("G/B")]
    public void Detects_StandardChordShapes(string chord)
    {
        // A line consisting solely of the chord must be recognised as a chord line.
        Assert.Contains(chord, ChordNames(chord + "  " + chord));
    }

    // ── Ordinary words are NOT chords ────────────────────────────────────────

    [Theory]
    [InlineData("Chorus")]
    [InlineData("Come")]
    [InlineData("Breathe")]
    [InlineData("And")]
    [InlineData("Bridge")]
    [InlineData("Each")]
    [InlineData("Face")]
    [InlineData("Ado")]
    public void Rejects_OrdinaryWordsStartingWithNoteLetters(string word)
    {
        Assert.DoesNotContain(word, ChordNames(word + "  " + word));
    }

    // ── Metadata header lines are not scanned for chords ─────────────────────

    [Fact]
    public void Ignores_TuningHeaderLine()
    {
        var names = ChordNames("Tuning: D A D G B E\n\nF#5/D  F#5/D");
        Assert.Contains("F#5/D", names);
        // The bare tuning notes must not leak in as chords.
        Assert.DoesNotContain("D", names);
        Assert.DoesNotContain("A", names);
        Assert.DoesNotContain("G", names);
        Assert.DoesNotContain("B", names);
        Assert.DoesNotContain("E", names);
    }

    [Fact]
    public void Ignores_KeyAndCapoHeaderLines()
    {
        var names = ChordNames("Key: F#\nCapo: No capo\n\nA5  B5  G5");
        Assert.DoesNotContain("F#", names);
        Assert.Contains("A5", names);
        Assert.Contains("B5", names);
        Assert.Contains("G5", names);
    }

    [Fact]
    public void Ignores_StandardTuningHeaderLine()
    {
        var names = ChordNames("Standard Tuning: E A D G B E\n\nG  C  D");
        Assert.DoesNotContain("E", names);
        Assert.Contains("G", names);
        Assert.Contains("C", names);
        Assert.Contains("D", names);
    }

    // ── Real-world corpus: every chord defined across the WikiTab library ────

    public static IEnumerable<object[]> WikiTabChords()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "wikitab-chords.txt");
        foreach (var line in File.ReadLines(path))
        {
            var chord = line.Trim();
            if (chord.Length == 0 || chord.StartsWith('#'))
            {
                continue;
            }
            yield return [chord];
        }
    }

    [Theory]
    [MemberData(nameof(WikiTabChords))]
    public void Recognises_EveryRealChordFromWikiTabLibrary(string chord)
    {
        // A line made solely of this chord must be detected as a chord line and
        // surface the chord — i.e. the regex must not reject real-world shapes.
        Assert.Contains(chord, ChordNames(chord + "  " + chord));
    }

    // ── Full Everlong fixture: regression for the reported bug ───────────────

    [Fact]
    public void Everlong_ProducesOnlyRealChords()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tabs", "Foo Fighters - Everlong.txt");
        Assert.True(File.Exists(path), $"fixture missing: {path}");

        var names = ChordNames(File.ReadAllText(path));

        string[] expected =
        [
            "F#5/D", "F#5/B", "D5/G", "C#/D", "D/D", "E/D", "F#/D", "G/D", "A5", "B5", "G5"
        ];
        Assert.Equal(expected.OrderBy(x => x), names.OrderBy(x => x));

        // Specifically none of the previously-leaking false positives.
        foreach (var bogus in new[] { "Chorus", "Come", "Breathe", "And", "F#", "A", "B", "E" })
            Assert.DoesNotContain(bogus, names);
    }
}
