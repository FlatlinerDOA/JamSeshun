using JamSeshun.Services;

namespace JamSeshun.Tests;

/// <summary>
/// Integration tests that run the parser over the full real-world WikiTab corpus
/// (214 *.Chords.txt files at the time this was written). Guarded so the whole
/// class passes silently when the local WikiTab directory is absent (CI stays green).
/// </summary>
public class WikiTabCorpusSpec
{
    private const string WikiTabDir = @"D:\ChizDev\WikiTab\Tabs";

    private static List<string> CorpusFiles() =>
        Directory.Exists(WikiTabDir)
            ? Directory.EnumerateFiles(WikiTabDir, "*.Chords.txt").ToList()
            : [];

    [Fact]
    public void Parsing_NeverThrows_ForAnyCorpusFile()
    {
        var files = CorpusFiles();
        if (files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var exception = Record.Exception(() =>
            {
                WikiTabParser.ParsePerformanceLines(content);
                WikiTabParser.ParseChords(content);
                WikiTabParser.ParseChordSequence(content);
                WikiTabParser.ParseLines(content);
            });

            Assert.True(exception is null, $"{Path.GetFileName(file)} threw: {exception}");
        }
    }

    [Fact]
    public void AtLeast95Percent_OfCorpusFiles_ProduceAtLeastOnePerformanceLine()
    {
        // Measured: 213 of 214 files (99.5%) produce >= 1 performance line after the
        // parser fixes in this change (up from 212/214 before). The one remaining file,
        // "David Gray - Last Boat To America V1.Chords.txt", uses a ChordPro-style
        // inline "[Chord]lyric" format that is out of scope for this change.
        var files = CorpusFiles();
        if (files.Count == 0)
        {
            return;
        }

        var withLines = files.Count(f => WikiTabParser.ParsePerformanceLines(File.ReadAllText(f)).Count > 0);

        var ratio = (double)withLines / files.Count;
        Assert.True(ratio >= 0.99, $"Only {withLines}/{files.Count} ({ratio:P1}) corpus files produced >= 1 performance line.");
    }

    [Fact]
    public void EveryFile_WithBracketSectionHeader_ProducesLabelledPerformanceLine()
    {
        var files = CorpusFiles();
        if (files.Count == 0)
        {
            return;
        }

        var bracketHeaderRegex = new System.Text.RegularExpressions.Regex(
            @"^\s*\[(Verse|Chorus|Intro)",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            if (!bracketHeaderRegex.IsMatch(content))
            {
                continue;
            }

            var lines = WikiTabParser.ParsePerformanceLines(content);
            Assert.True(
                lines.Any(l => l.SectionLabel.Length > 0),
                $"{Path.GetFileName(file)} has a [Verse]/[Chorus]/[Intro] header but no labelled performance line.");
        }
    }

    [Fact]
    public void NoPerformanceLine_HasLyricTextThatLooksLikeASectionHeader()
    {
        var files = CorpusFiles();
        if (files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            var lines = WikiTabParser.ParsePerformanceLines(File.ReadAllText(file));
            foreach (var line in lines)
            {
                var trimmed = line.LyricText.Trim();
                var looksLikeSectionHeader = trimmed.StartsWith('[') && trimmed.EndsWith(']');
                Assert.False(looksLikeSectionHeader, $"{Path.GetFileName(file)}: lyric text '{trimmed}' looks like a section header.");
            }
        }
    }

    [Fact]
    public void NoPerformanceLine_ContainsBogusChordTokens()
    {
        var files = CorpusFiles();
        if (files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            var lines = WikiTabParser.ParsePerformanceLines(File.ReadAllText(file));
            foreach (var chord in lines.SelectMany(l => l.Chords))
            {
                Assert.False(
                    chord.Name.Equals("N.C", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)}: found bogus chord 'N.C'.");
                Assert.False(
                    chord.Name.Equals("x2", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)}: found bogus chord 'x2'.");
                Assert.False(
                    chord.Name.Equals("2x", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)}: found bogus chord '2x'.");
                Assert.False(
                    chord.Name.StartsWith('|'),
                    $"{Path.GetFileName(file)}: found bogus chord starting with '|': '{chord.Name}'.");
            }
        }
    }
}
