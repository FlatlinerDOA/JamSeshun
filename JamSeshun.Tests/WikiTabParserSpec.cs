using JamSeshun.Services;

namespace JamSeshun.Tests;

public class WikiTabParserSpec
{
    // ── Filename parsing ────────────────────────────────────────────────────

    [Theory]
    [InlineData("David Gray - Babylon V1.Chords.txt",               "David Gray",  "Babylon")]
    [InlineData("David Bowie - Space Oddity V2.Chords.txt",         "David Bowie", "Space Oddity")]
    [InlineData("Eagles - Hotel California V1.Chords.txt",          "Eagles",      "Hotel California")]
    [InlineData("Ed Sheeran - Perfect V1.Chords.txt",               "Ed Sheeran",  "Perfect")]
    [InlineData("Ed Sheeran - How Would You Feel Paean V1.Chords.txt", "Ed Sheeran", "How Would You Feel Paean")]
    public void Parse_ExtractsArtistAndSong(string filename, string expectedArtist, string expectedSong)
    {
        var result = WikiTabParser.Parse(filename, string.Empty);
        Assert.NotNull(result);
        Assert.Equal(expectedArtist, result.Artist);
        Assert.Equal(expectedSong,   result.Song);
    }

    [Fact]
    public void Parse_ReturnsNull_ForUnrecognisedFilename()
    {
        Assert.Null(WikiTabParser.Parse("random-file.txt", string.Empty));
        Assert.Null(WikiTabParser.Parse("NoDashSeparator V1.Chords.txt", string.Empty));
    }

    // ── Capo extraction ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Capo: 1",               1)]
    [InlineData("Capo: 2",               2)]
    [InlineData("Capo: (None)",          0)]
    [InlineData("Capo 2",                2)]
    [InlineData("CAPO 2",                2)]
    [InlineData("Capo on first fret",    1)]
    [InlineData("Capo on the 3rd fret",  3)]
    [InlineData("Capo: 1st fret",        1)]
    [InlineData("Capo: 2nd fret",        2)]
    public void Parse_ExtractsCapo(string capoLine, int expectedCapo)
    {
        var result = WikiTabParser.Parse("Artist - Song V1.Chords.txt", capoLine + "\n\nContent");
        Assert.NotNull(result);
        Assert.Equal(expectedCapo, result.Capo);
    }

    [Fact]
    public void Parse_DefaultsCapoToZero_WhenAbsent()
    {
        var result = WikiTabParser.Parse("Artist - Song V1.Chords.txt", "[Verse]\nG  C  D");
        Assert.NotNull(result);
        Assert.Equal(0, result.Capo);
    }

    // ── Tuning extraction ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Standard Tuning: E A D G B E",  "E A D G B E")]
    [InlineData("Tuning: E A D G B E",            "E A D G B E")]
    [InlineData("standard tuning: D A D G B E",   "D A D G B E")]
    public void Parse_ExtractsTuning(string tuningLine, string expectedTuning)
    {
        var result = WikiTabParser.Parse("Artist - Song V1.Chords.txt", tuningLine + "\n\nContent");
        Assert.NotNull(result);
        Assert.Equal(expectedTuning, result.Tuning);
    }

    [Fact]
    public void Parse_DefaultsTuningToEmpty_WhenAbsent()
    {
        var result = WikiTabParser.Parse("Artist - Song V1.Chords.txt", "Capo 2\n\n[Verse]\nG C D");
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Tuning);
    }

    // ── Full file content stored as-is ──────────────────────────────────────

    [Fact]
    public void Parse_StoresRawContentUnchanged()
    {
        const string content = "Standard Tuning: E A D G B E\nCapo: 1\n\n[Verse]\nG  C\nSome lyrics";
        var result = WikiTabParser.Parse("Artist - Song V1.Chords.txt", content);
        Assert.NotNull(result);
        Assert.Equal(content, result.Content);
    }

    // ── Real-world header samples ────────────────────────────────────────────

    [Fact]
    public void Parse_BabylonHeader()
    {
        const string content = "Capo on first fret\n\n\nIntro:   Dmaj9/F#  G  Dmaj9/F#  G\n\nVerse 1:";
        var result = WikiTabParser.Parse("David Gray - Babylon V1.Chords.txt", content);
        Assert.NotNull(result);
        Assert.Equal("David Gray", result.Artist);
        Assert.Equal("Babylon",    result.Song);
        Assert.Equal(1,            result.Capo);
        Assert.Equal(string.Empty, result.Tuning);
    }

    [Fact]
    public void Parse_SpaceOddityHeader()
    {
        const string content = "Standard Tuning: E A D G B E\nCapo: (None)\nChords:\nFmaj7:[xx3210]\n\n[Intro]";
        var result = WikiTabParser.Parse("David Bowie - Space Oddity V1.Chords.txt", content);
        Assert.NotNull(result);
        Assert.Equal("David Bowie",  result.Artist);
        Assert.Equal("Space Oddity", result.Song);
        Assert.Equal(0,              result.Capo);
        Assert.Equal("E A D G B E",  result.Tuning);
    }

    [Fact]
    public void Parse_HotelCaliforniaHeader()
    {
        const string content = "Tabbed by: Emrldeyzs\nCAPO 2\n\n[Verse]\nAm";
        var result = WikiTabParser.Parse("Eagles - Hotel California V1.Chords.txt", content);
        Assert.NotNull(result);
        Assert.Equal(2, result.Capo);
    }

    [Fact]
    public void Parse_PerfectHeader_DuplicateCapoLine_TakesFirst()
    {
        // Perfect V1 has both "Capo: 1" and "Capo: 1st fret" — either is fine, just be consistent
        const string content = "Standard Tuning: E A D G B E\nCapo: 1\nCapo: 1st fret\nPlay: G\n\n[Verse]";
        var result = WikiTabParser.Parse("Ed Sheeran - Perfect V1.Chords.txt", content);
        Assert.NotNull(result);
        Assert.Equal(1, result.Capo);
    }

    // ── Integration: real WikiTab directory ─────────────────────────────────

    private const string WikiTabDir = @"D:\ChizDev\WikiTab\Tabs";

    [Fact]
    public void ParseDirectory_AllFilesParseWithoutError()
    {
        if (!Directory.Exists(WikiTabDir))
            return; // not available in CI

        var tabs = WikiTabParser.ParseDirectory(WikiTabDir).ToList();
        Assert.NotEmpty(tabs);
        Assert.All(tabs, t =>
        {
            Assert.NotEmpty(t.Artist);
            Assert.NotEmpty(t.Song);
            Assert.NotNull(t.Content);
        });
    }

    [Fact]
    public void ParseDirectory_BabylonHasCapo1()
    {
        if (!Directory.Exists(WikiTabDir)) return;

        var babylon = WikiTabParser.ParseDirectory(WikiTabDir)
            .FirstOrDefault(t => t.Artist == "David Gray" && t.Song == "Babylon");
        Assert.NotNull(babylon);
        Assert.Equal(1, babylon!.Capo);
    }

    [Fact]
    public void ParseDirectory_SpaceOddityHasStandardTuning()
    {
        if (!Directory.Exists(WikiTabDir)) return;

        var tab = WikiTabParser.ParseDirectory(WikiTabDir)
            .FirstOrDefault(t => t.Artist == "David Bowie" && t.Song == "Space Oddity");
        Assert.NotNull(tab);
        Assert.Equal("E A D G B E", tab!.Tuning);
        Assert.Equal(0, tab.Capo);
    }
}
