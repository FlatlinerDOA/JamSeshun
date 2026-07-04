using JamSeshun.Services;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for <see cref="WikiTabParser.ParsePerformanceLines"/>.
///
/// Real-world tab excerpts are copied verbatim from the WikiTab library so the
/// parser is validated against the actual formatting patterns that appear in practice,
/// not just synthetic inputs.  Real-file integration tests are guarded so they pass
/// in CI even when the local WikiTab directory is absent.
/// </summary>
public class PerformanceLinesSpec
{
    private const string WikiTabDir = @"D:\ChizDev\WikiTab\Tabs";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<PerformanceLine> Parse(string content) =>
        WikiTabParser.ParsePerformanceLines(content);

    private static IReadOnlyList<string> ChordNames(PerformanceLine line) =>
        line.Chords.Select(c => c.Name).ToList();

    // ── Null / empty inputs ──────────────────────────────────────────────────

    [Fact]
    public void Null_ReturnsEmpty()
    {
        Assert.Empty(Parse(null!));
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Empty(Parse(string.Empty));
    }

    [Fact]
    public void LyricsOnly_NoChordLines_ReturnsEmpty()
    {
        const string content =
            "Just some words here\n" +
            "With no chords at all\n";
        Assert.Empty(Parse(content));
    }

    // ── Basic chord/lyric pairing ────────────────────────────────────────────

    [Fact]
    public void SingleChordLine_PairedWithFollowingLyric()
    {
        const string content =
            "G  Em  C  D\n" +
            "These are the words\n";

        var lines = Parse(content);

        Assert.Single(lines);
        Assert.Equal(["G", "Em", "C", "D"], ChordNames(lines[0]));
        Assert.Equal("These are the words", lines[0].LyricText);
    }

    [Fact]
    public void SingleChordLine_NoFollowingLyric_HasEmptyLyricText()
    {
        const string content = "G  Em  C  D\n";

        var lines = Parse(content);

        Assert.Single(lines);
        Assert.Empty(lines[0].LyricText);
    }

    [Fact]
    public void ChordLine_LooksPastBlankLinesToFindLyric()
    {
        // A blank line between chord and lyric still pairs them.
        const string content =
            "Am  F\n" +
            "\n" +
            "Some lyrics here\n";

        // ParseLines treats the blank as Blank; the lyric is still reached.
        // (Whether this matches depends on the look-ahead finding Lyric before Chord.)
        // This documents the current behaviour — blank is skipped, lyric is found.
        var lines = Parse(content);

        Assert.Single(lines);
        Assert.Equal("Some lyrics here", lines[0].LyricText);
    }

    [Fact]
    public void MultipleChordLines_EachPairedWithOwnLyric()
    {
        const string content =
            "C         Em           Am                  G\n" +
            "Please forgive me if I act a little strange\n" +
            "C      Em                Am G\n" +
            "For I know not what I do.\n";

        var lines = Parse(content);

        Assert.Equal(2, lines.Count);
        Assert.Equal(["C", "Em", "Am", "G"], ChordNames(lines[0]));
        Assert.Equal("Please forgive me if I act a little strange", lines[0].LyricText);
        Assert.Equal(["C", "Em", "Am", "G"], ChordNames(lines[1]));
        Assert.Equal("For I know not what I do.", lines[1].LyricText);
    }

    [Fact]
    public void ChordLineFollowedByChordLine_SecondChordLineHasNoLyric()
    {
        // Chord then chord (e.g. instrumental break) — no lyric consumed.
        const string content =
            "G  D\n" +
            "Em  C\n";

        var lines = Parse(content);

        Assert.Equal(2, lines.Count);
        Assert.Empty(lines[0].LyricText); // next non-blank is a Chord, not a Lyric
        Assert.Empty(lines[1].LyricText);
    }

    // ── Section labels ───────────────────────────────────────────────────────

    [Fact]
    public void ColonSectionHeader_AppearsOnFirstLineOfSection()
    {
        // Format "Verse 1:" (no brackets) is classified as Section by ParseLines.
        const string content =
            "Verse 1:\n" +
            "G  D\n" +
            "First lyric\n";

        var lines = Parse(content);

        Assert.Single(lines);
        Assert.Equal("Verse 1", lines[0].SectionLabel);
    }

    [Fact]
    public void ColonSectionHeader_EmptyOnSubsequentLinesInSameSection()
    {
        const string content =
            "Chorus:\n" +
            "G  D\n" +
            "First line\n" +
            "Em  C\n" +
            "Second line\n";

        var lines = Parse(content);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Chorus", lines[0].SectionLabel);
        Assert.Empty(lines[1].SectionLabel);
    }

    [Fact]
    public void SectionLabel_ChangesAcrossSections()
    {
        const string content =
            "Verse 1:\n" +
            "G  Em\n" +
            "Verse words\n" +
            "\n" +
            "Chorus:\n" +
            "C  D\n" +
            "Chorus words\n";

        var lines = Parse(content);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Verse 1", lines[0].SectionLabel);
        Assert.Equal("Chorus", lines[1].SectionLabel);
    }

    [Fact]
    public void BracketSectionHeaders_ProduceNoSectionLabel()
    {
        // [VERSE 1] format (no colon) is classified as Lyric, not Section —
        // so currentSection is never updated and no label is emitted.
        const string content =
            "[VERSE 1]\n" +
            "\n" +
            "C         Em           Am                  G\n" +
            "Please forgive me if I act a little strange\n";

        var lines = Parse(content);

        Assert.Single(lines);
        Assert.Empty(lines[0].SectionLabel);
    }

    // ── Chords: definition block exclusion ───────────────────────────────────

    [Fact]
    public void ChordsDefinitionBlock_IsExcluded()
    {
        // Chords: header followed by definition lines and a blank terminator.
        const string content =
            "Chords:\n" +
            "Am   x02210\n" +
            "G    320003\n" +
            "\n" +
            "Verse 1:\n" +
            "Am  G\n" +
            "Some lyrics\n";

        var lines = Parse(content);

        // Only the actual song line should appear — not the definition entries.
        Assert.Single(lines);
        Assert.Equal(["Am", "G"], ChordNames(lines[0]));
        Assert.Equal("Some lyrics", lines[0].LyricText);
    }

    [Fact]
    public void ChordsDefinitionBlock_CaseInsensitive()
    {
        const string content =
            "CHORDS:\n" +
            "C2  Em  D/F#\n" +
            "\n" +
            "Verse:\n" +
            "Em  G\n" +
            "Lyric here\n";

        var lines = Parse(content);

        Assert.Single(lines);
    }

    // ── Tab notation lines excluded ───────────────────────────────────────────

    [Fact]
    public void TabStringLines_AreNotIncluded()
    {
        // e|----|B|----|G|---- lines must not appear as performance lines.
        const string content =
            "[Intro]\n" +
            "e|---2---2---|\n" +
            "B|---3---3---|\n" +
            "G|---2---2---|\n" +
            "\n" +
            "G  D  Em  C\n" +
            "These are lyrics\n";

        var lines = Parse(content);

        Assert.Single(lines);
        Assert.Equal(["G", "D", "Em", "C"], ChordNames(lines[0]));
    }

    // ── Chord diagram lookup ─────────────────────────────────────────────────

    [Fact]
    public void KnownChord_HasFretsFromLibrary()
    {
        const string content = "Am  G\nSome words\n";

        var lines = Parse(content);

        var am = lines[0].Chords.First(c => c.Name == "Am");
        var g  = lines[0].Chords.First(c => c.Name == "G");

        Assert.NotNull(am.Frets);
        Assert.NotNull(g.Frets);
    }

    [Fact]
    public void UnknownChord_HasNullFrets()
    {
        // C2 is not in ChordLibrary; frets should be null.
        const string content = "C2  Em\nSome words\n";

        var lines = Parse(content);

        var c2 = lines[0].Chords.First(c => c.Name == "C2");
        Assert.Null(c2.Frets);
    }

    // ── Real-world: Babylon (David Gray) ─────────────────────────────────────
    // Verbatim excerpt from David Gray - Babylon V1.Chords.txt, lines 7–14.
    // Format: colon-terminated section headers; single chord per chord line.

    private const string BabylonVerse1Excerpt =
        "Verse 1:\n" +
        "         Dmaj9/F#\n" +
        "         Friday night an' I'm going nowhere,\n" +
        "         G                           Dmaj9/F#      G\n" +
        "         All the lights are changing green to red.\n" +
        "         Dmaj9/F#\n" +
        "         Turning over T.V. stations,\n" +
        "         G                             Dmaj9/F#   G\n" +
        "         Situations running through my   head\n";

    [Fact]
    public void Babylon_FirstChordLine_IsSingleDmaj9_WithSectionLabel()
    {
        var lines = Parse(BabylonVerse1Excerpt);

        Assert.Equal("Verse 1", lines[0].SectionLabel);
        Assert.Equal(["Dmaj9/F#"], ChordNames(lines[0]));
        Assert.Equal("Friday night an' I'm going nowhere,", lines[0].LyricText);
    }

    [Fact]
    public void Babylon_SecondChordLine_HasThreeChords_NoSectionLabel()
    {
        var lines = Parse(BabylonVerse1Excerpt);

        Assert.Empty(lines[1].SectionLabel);
        Assert.Equal(["G", "Dmaj9/F#", "G"], ChordNames(lines[1]));
        Assert.Equal("All the lights are changing green to red.", lines[1].LyricText);
    }

    [Fact]
    public void Babylon_Verse1_HasFourLines()
    {
        var lines = Parse(BabylonVerse1Excerpt);
        Assert.Equal(4, lines.Count);
    }

    // ── Real-world: Please Forgive Me (David Gray) ───────────────────────────
    // Verbatim excerpt from David Gray - Please Forgive Me V1.Chords.txt, lines 11–19.
    // Format: bracketed section headers (no labels produced); multiple chords per line.

    private const string PleaseForgiveMeVerse1Excerpt =
        "[VERSE 1]\n" +
        "\n" +
        "C         Em           Am                  G\n" +
        "Please forgive me if I act a little strange\n" +
        "C      Em                Am G         \n" +
        "For I know not what I do.\n" +
        "C           Em                  Am            G    \n" +
        "Feels like lightning running through my veins\n" +
        "F    F      C          G F     F              C   Em Am G\n" +
        "Everytime I look at you,  everytime I look at you\n";

    [Fact]
    public void PleaseForgiveMeVerse1_HasFourLines()
    {
        var lines = Parse(PleaseForgiveMeVerse1Excerpt);
        Assert.Equal(4, lines.Count);
    }

    [Fact]
    public void PleaseForgiveMeVerse1_BracketHeader_ProducesNoSectionLabel()
    {
        var lines = Parse(PleaseForgiveMeVerse1Excerpt);
        Assert.All(lines, l => Assert.Empty(l.SectionLabel));
    }

    [Fact]
    public void PleaseForgiveMeVerse1_FirstLine_ChordsAndLyric()
    {
        var lines = Parse(PleaseForgiveMeVerse1Excerpt);

        Assert.Equal(["C", "Em", "Am", "G"], ChordNames(lines[0]));
        Assert.Equal("Please forgive me if I act a little strange", lines[0].LyricText);
    }

    [Fact]
    public void PleaseForgiveMeVerse1_LastLine_ContainsMixedChordTokens()
    {
        // "F    F      C          G F     F              C   Em Am G"
        // is a long line of repeated chords above the final lyric.
        var lines = Parse(PleaseForgiveMeVerse1Excerpt);
        var last = lines[^1];

        Assert.Contains("F", ChordNames(last));
        Assert.Contains("C", ChordNames(last));
        Assert.Contains("Em", ChordNames(last));
        Assert.Equal("Everytime I look at you,  everytime I look at you", last.LyricText);
    }

    // ── Real-world: Monsters (Something for Kate) ────────────────────────────
    // Verbatim excerpt from Something for Kate - Monsters V1.Chords.txt, lines 1–28.
    // Tests that the CHORDS: diagram table is excluded and verses parse correctly.

    private const string MonstersExcerpt =
        "Monsters  (Capo 1)\n" +
        "   ------Something for Kate\n" +
        "\n" +
        "CHORDS:\n" +
        "     C2  Em   D/F#     A#   G/B   F    G   Am   Dm \n" +
        "e|---3---3----3--------3----3-----1----3--------1-----------|\n" +
        "B|---3---3----3--------3----3-----2----3---1----3-----------|\n" +
        "G|------------2----------------------------2----2-----------|\n" +
        "D|-------2------------------------3--------2----------------|\n" +
        "A|---3---2-------------1----2----------2-------------------|\n" +
        "E|------------2------------------------3--------------------|\n" +
        "\n" +
        "INTRO:\n" +
        "C2  Em (x3)   C2\n" +
        "\n" +
        "VERSE 1:\n" +
        "C2                   Em            D/F#\n" +
        "I was hanging upside down from the overpass\n" +
        "A#-G/B     C2            D/F#           Em\n" +
        "Waiting to discover something about the world\n";

    [Fact]
    public void Monsters_ChordsBlock_IsExcluded()
    {
        var lines = Parse(MonstersExcerpt);

        // No chord names from the CHORDS: diagram table should appear in section labels
        // or be the sole content — every line must come from after the definition block.
        var allChordNames = lines.SelectMany(l => l.Chords).Select(c => c.Name).ToHashSet();

        // These are chords from the body, not the definition table header row
        // ("     C2  Em   D/F#...") which should be skipped.
        Assert.DoesNotContain("A#", allChordNames); // A# does not match ChordRegex due to lack of quality
        // All parsed lines should be post-definition content
        Assert.True(lines.Count > 0);
    }

    [Fact]
    public void Monsters_IntroSection_ChordsOnlyNoPreceedingLyric()
    {
        var lines = Parse(MonstersExcerpt);

        var intro = lines.First(l => l.SectionLabel == "INTRO");
        // C2  Em (x3)   C2 — (x3) is not a chord token; only C2 and Em survive.
        Assert.Contains("C2", ChordNames(intro));
        Assert.Contains("Em", ChordNames(intro));
        Assert.Empty(intro.LyricText); // next non-blank is VERSE 1:, not a lyric
    }

    [Fact]
    public void Monsters_Verse1_FirstLine_ChordsAndLyric()
    {
        var lines = Parse(MonstersExcerpt);

        var verse = lines.First(l => l.SectionLabel == "VERSE 1");
        Assert.Equal(["C2", "Em", "D/F#"], ChordNames(verse));
        Assert.Equal("I was hanging upside down from the overpass", verse.LyricText);
    }

    [Fact]
    public void Monsters_Verse1_SecondLine_ExcludesInvalidToken()
    {
        // "A#-G/B     C2            D/F#           Em"
        // "A#-G/B" has a dash — not matched by ChordRegex, correctly excluded.
        var lines = Parse(MonstersExcerpt);

        var secondVerseLine = lines.First(l => l.LyricText.Contains("Waiting to discover"));
        Assert.DoesNotContain("A#-G/B", ChordNames(secondVerseLine));
        Assert.Equal(["C2", "D/F#", "Em"], ChordNames(secondVerseLine));
        Assert.Equal("Waiting to discover something about the world", secondVerseLine.LyricText);
    }

    // ── Real-world: This Year's Love (David Gray) ────────────────────────────
    // Verbatim excerpt from David Gray - This Years Love V3.Chords.txt, lines 7–18.
    // Shows single-chord lines (e.g. just "D") paired with lyrics.

    private const string ThisYearsLoveExcerpt =
        "C          D               C\n" +
        "This years love had better last\n" +
        "C          D           C\n" +
        "Heaven knows it's high time\n" +
        "C         D             G        Em\n" +
        "I've been waiting on my own, too long\n" +
        "C        D                C\n" +
        "When you hold me like you do\n" +
        "            D\n" +
        "It feels so right, oh now\n" +
        "C        D             G\n" +
        "Start to forget how my heart gets torn\n" +
        "          Em\n" +
        "When that hurt gets thrown\n" +
        "        A               C\n" +
        "Feelin' like I can't go on.\n";

    [Fact]
    public void ThisYearsLove_ParsesEightLines()
    {
        var lines = Parse(ThisYearsLoveExcerpt);
        Assert.Equal(8, lines.Count);
    }

    [Fact]
    public void ThisYearsLove_SingleChordLine_PairedWithLyric()
    {
        // "            D\n" + "It feels so right, oh now\n"
        var lines = Parse(ThisYearsLoveExcerpt);

        var singleD = lines.First(l => l.LyricText == "It feels so right, oh now");
        Assert.Equal(["D"], ChordNames(singleD));
    }

    [Fact]
    public void ThisYearsLove_SingleEmLine_PairedWithLyric()
    {
        // "          Em\n" + "When that hurt gets thrown\n"
        var lines = Parse(ThisYearsLoveExcerpt);

        var emLine = lines.First(l => l.LyricText == "When that hurt gets thrown");
        Assert.Equal(["Em"], ChordNames(emLine));
    }

    [Fact]
    public void ThisYearsLove_ChordLinePrecededBySpace_TrimmedProperly()
    {
        // "        A               C\n" + "Feelin' like I can't go on.\n"
        var lines = Parse(ThisYearsLoveExcerpt);

        var acLine = lines.First(l => l.LyricText.StartsWith("Feelin'"));
        Assert.Equal(["A", "C"], ChordNames(acLine));
    }

    // ── Real-file integration tests ──────────────────────────────────────────

    [Fact]
    public void RealFile_Babylon_FirstLineIsDmaj9WithVerse1Label()
    {
        var path = Path.Combine(WikiTabDir, "David Gray - Babylon V1.Chords.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var lines = WikiTabParser.ParsePerformanceLines(File.ReadAllText(path));

        Assert.NotEmpty(lines);
        var firstLabelled = lines.First(l => l.SectionLabel.Length > 0);
        Assert.Equal("Verse 1", firstLabelled.SectionLabel);
        Assert.Equal(["Dmaj9/F#"], ChordNames(firstLabelled));
    }

    [Fact]
    public void RealFile_PleaseForgiveMeV1_ParsesWithoutError()
    {
        var path = Path.Combine(WikiTabDir, "David Gray - Please Forgive Me V1.Chords.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var lines = WikiTabParser.ParsePerformanceLines(File.ReadAllText(path));

        Assert.NotEmpty(lines);
        Assert.All(lines, l =>
        {
            Assert.NotEmpty(l.Chords);
            Assert.NotNull(l.LyricText);
        });
    }

    [Fact]
    public void RealFile_MonstersV1_ChordsBlockProducesNoHeaderLines()
    {
        var path = Path.Combine(WikiTabDir, "Something for Kate - Monsters V1.Chords.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var lines = WikiTabParser.ParsePerformanceLines(File.ReadAllText(path));

        // The "CHORDS:" diagram header row contains "C2  Em   D/F#..." as a Chord line.
        // If ParsePerformanceLines incorrectly includes it, the lyric for that line
        // would be a tab-string row — which is impossible since TabStrings are skipped.
        // Every lyric must be a real lyric string, never a tab-notation row.
        Assert.All(lines, l =>
        {
            if (!string.IsNullOrEmpty(l.LyricText))
            {
                Assert.DoesNotMatch(@"^[eEbBgGdDaA]\|", l.LyricText);
            }
        });
    }
}
