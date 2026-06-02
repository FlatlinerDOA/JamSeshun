using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

public class GuitarTuningSpec
{
    // ── Note-sequence parsing ────────────────────────────────────────────────

    [Theory]
    [InlineData("E A D G B E")]
    [InlineData("D A D G B E")]
    [InlineData("D A D G A D")]
    [InlineData("Eb Ab Db Gb Bb Eb")]
    [InlineData("D G D G B D")]
    public void TryParse_ValidSixNoteSequence_ReturnsTuningWith6Strings(string input)
    {
        var result = GuitarTuning.TryParse(input);
        Assert.NotNull(result);
        Assert.Equal(6, result.Strings.Length);
    }

    [Fact]
    public void TryParse_StandardSequence_ReturnsCorrectStrings()
    {
        var result = GuitarTuning.TryParse("E A D G B E");
        Assert.NotNull(result);
        Assert.Equal("E",  result.Strings[0].Name); Assert.Equal(2, result.Strings[0].Octave);
        Assert.Equal("A",  result.Strings[1].Name); Assert.Equal(2, result.Strings[1].Octave);
        Assert.Equal("D",  result.Strings[2].Name); Assert.Equal(3, result.Strings[2].Octave);
        Assert.Equal("G",  result.Strings[3].Name); Assert.Equal(3, result.Strings[3].Octave);
        Assert.Equal("B",  result.Strings[4].Name); Assert.Equal(3, result.Strings[4].Octave);
        Assert.Equal("E",  result.Strings[5].Name); Assert.Equal(4, result.Strings[5].Octave);
    }

    [Fact]
    public void TryParse_DropDSequence_HasLowD()
    {
        var result = GuitarTuning.TryParse("D A D G B E");
        Assert.NotNull(result);
        Assert.Equal("D", result.Strings[0].Name);
        Assert.Equal(2,   result.Strings[0].Octave);
        // Strings 1-5 match Standard
        Assert.Equal("A", result.Strings[1].Name);
        Assert.Equal("E", result.Strings[5].Name);
    }

    [Fact]
    public void TryParse_DadgadSequence_HasCorrectStrings()
    {
        var result = GuitarTuning.TryParse("D A D G A D");
        Assert.NotNull(result);
        Assert.Equal("D", result.Strings[0].Name);
        Assert.Equal("A", result.Strings[4].Name); Assert.Equal(3, result.Strings[4].Octave);
        Assert.Equal("D", result.Strings[5].Name); Assert.Equal(4, result.Strings[5].Octave);
    }

    [Fact]
    public void TryParse_FlatSequence_MapsEnharmonics()
    {
        // Eb Ab Db Gb Bb Eb — flats stored as their sharp enharmonic equivalents in Note.BaseNotes
        var result = GuitarTuning.TryParse("Eb Ab Db Gb Bb Eb");
        Assert.NotNull(result);
        Assert.Equal("Eb", result.Strings[0].Name); // Eb stays Eb
        Assert.Equal("G#", result.Strings[1].Name); // Ab → G#
        Assert.Equal("C#", result.Strings[2].Name); // Db → C#
        Assert.Equal("F#", result.Strings[3].Name); // Gb → F#
        Assert.Equal("Bb", result.Strings[4].Name); // Bb stays Bb
        Assert.Equal("Eb", result.Strings[5].Name);
    }

    // ── Named tuning shortcuts ───────────────────────────────────────────────

    [Theory]
    [InlineData("Standard")]
    [InlineData("STANDARD")]
    [InlineData("EADGBE")]
    [InlineData("Drop D")]
    [InlineData("drop d")]
    [InlineData("DADGAD")]
    [InlineData("dadgad")]
    [InlineData("Open G")]
    [InlineData("Open D")]
    [InlineData("Eb Standard")]
    [InlineData("Half Step Down")]
    public void TryParse_KnownName_ReturnsNonNull(string input)
    {
        Assert.NotNull(GuitarTuning.TryParse(input));
    }

    [Fact]
    public void TryParse_StandardName_MatchesStandardSequence()
    {
        var byName     = GuitarTuning.TryParse("Standard");
        var bySequence = GuitarTuning.TryParse("E A D G B E");

        Assert.NotNull(byName);
        Assert.NotNull(bySequence);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(byName.Strings[i].Name,   bySequence.Strings[i].Name);
            Assert.Equal(byName.Strings[i].Octave, bySequence.Strings[i].Octave);
        }
    }

    // ── Null / invalid input ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("E A D G")]          // too few notes
    [InlineData("E A D G B E C")]    // too many notes
    [InlineData("X X X X X X")]      // invalid note names
    [InlineData("1 2 3 4 5 6")]      // numbers
    public void TryParse_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(GuitarTuning.TryParse(input));
    }

    // ── Integration: actual tab files ────────────────────────────────────────

    private static string TabPath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Tabs", filename);

    [Fact]
    public void Everlong_WikiTabParser_ExtractsDropDTuning()
    {
        var path = TabPath("Foo Fighters - Everlong.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        var tab = WikiTabParser.Parse("Foo Fighters - Everlong V1.Chords.txt", content);

        Assert.NotNull(tab);
        Assert.Equal("D A D G B E", tab.Tuning);
    }

    [Fact]
    public void Everlong_TuningParsesToDropD()
    {
        var path = TabPath("Foo Fighters - Everlong.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        var tab = WikiTabParser.Parse("Foo Fighters - Everlong V1.Chords.txt", content);

        var tuning = GuitarTuning.TryParse(tab?.Tuning);
        Assert.NotNull(tuning);
        Assert.Equal("D", tuning.Strings[0].Name); // low string is D2
        Assert.Equal(2,   tuning.Strings[0].Octave);
        Assert.Equal("E", tuning.Strings[5].Name); // high string is E4
    }

    [Fact]
    public void RainSong_WikiTabParser_ExtractsStudioTuning()
    {
        var path = TabPath("Led Zeppelin - Rain Song.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        var tab = WikiTabParser.Parse("Led Zeppelin - Rain Song V1.Chords.txt", content);

        Assert.NotNull(tab);
        // "Studio Tuning: D G C G C D" takes priority over the generic "Tuning: E A D G B E" header
        Assert.Equal("D G C G C D", tab.Tuning);
    }

    [Fact]
    public void RainSong_TuningParsesToDGCGCD()
    {
        var path = TabPath("Led Zeppelin - Rain Song.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        var tab = WikiTabParser.Parse("Led Zeppelin - Rain Song V1.Chords.txt", content);

        var tuning = GuitarTuning.TryParse(tab?.Tuning);
        Assert.NotNull(tuning);
        Assert.Equal("D", tuning.Strings[0].Name); // low D
        Assert.Equal("G", tuning.Strings[1].Name);
        Assert.Equal("C", tuning.Strings[2].Name);
        Assert.Equal("G", tuning.Strings[3].Name);
        Assert.Equal("C", tuning.Strings[4].Name);
        Assert.Equal("D", tuning.Strings[5].Name); // high D
    }
}
