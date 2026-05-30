using System.Text.RegularExpressions;

namespace JamSeshun.Services;

public static class WikiTabParser
{
    private static readonly Regex FilenameRegex = new(
        @"^(?<artist>.+?) - (?<song>.+?) V(?<version>\d+)\.(?<type>[^.]+)\.txt$",
        RegexOptions.Compiled);

    // Matches: Capo: 1 | Capo: 1st fret | Capo: (None) | Capo 2 | CAPO 2 | Capo on first fret
    private static readonly Regex CapoRegex = new(
        @"^\s*capo\s*:?\s*(?:\((?<none>none)\)|on\s+(?:the\s+)?(?<ordword>\w+)\s+fret|(?<ordword2>\w+)\s+fret|(?<num>\d+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TuningRegex = new(
        @"^\s*(?:standard\s+)?tuning\s*:\s*(?<tuning>[A-Ga-g#b ]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Parses a WikiTab .txt file into a SavedTab. Returns null if the filename doesn't match the expected pattern.</summary>
    public static SavedTab? ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(Path.GetFileName(filePath), content);
    }

    /// <summary>Parses a filename + raw content into a SavedTab.</summary>
    public static SavedTab? Parse(string fileName, string content)
    {
        var m = FilenameRegex.Match(fileName);
        if (!m.Success) return null;

        var artist = m.Groups["artist"].Value.Trim();
        var song   = m.Groups["song"].Value.Trim();

        var (tuning, capo) = ParseMetadata(content);
        return new SavedTab(artist, song, content, tuning, capo, DateTimeOffset.Now);
    }

    /// <summary>Returns the version number from the filename (e.g. "Song V3.Chords.txt" → 3), or 1 if absent.</summary>
    public static int ParseVersion(string fileName)
    {
        var m = FilenameRegex.Match(fileName);
        return m.Success && int.TryParse(m.Groups["version"].Value, out var v) ? v : 1;
    }

    /// <summary>Returns the library store key for a tab: "Artist - Song" for V1, "Artist - Song V2" for higher versions.</summary>
    public static string StoreKey(string artist, string song, int version) =>
        version > 1 ? $"{artist} - {song} V{version}" : $"{artist} - {song}";

    /// <summary>Parses all matching .txt files in a directory.</summary>
    public static IEnumerable<SavedTab> ParseDirectory(string directoryPath) =>
        Directory.EnumerateFiles(directoryPath, "*.txt")
                 .Select(ParseFile)
                 .OfType<SavedTab>();

    private static (string Tuning, int Capo) ParseMetadata(string content)
    {
        var tuning = string.Empty;
        var capo = 0;

        // Only scan the first 25 lines — metadata is always in the header.
        foreach (var raw in content.Split('\n').Take(25))
        {
            var line = raw.TrimEnd('\r');

            var capoMatch = CapoRegex.Match(line);
            if (capoMatch.Success)
            {
                if (capoMatch.Groups["none"].Success)
                    capo = 0;
                else if (capoMatch.Groups["num"].Success)
                    int.TryParse(capoMatch.Groups["num"].Value, out capo);
                else if (capoMatch.Groups["ordword"].Success)
                    capo = ParseOrdinal(capoMatch.Groups["ordword"].Value);
                else if (capoMatch.Groups["ordword2"].Success)
                    capo = ParseOrdinal(capoMatch.Groups["ordword2"].Value);
                continue;
            }

            var tuningMatch = TuningRegex.Match(line);
            if (tuningMatch.Success)
            {
                tuning = tuningMatch.Groups["tuning"].Value.Trim();
                continue;
            }
        }

        return (tuning, capo);
    }

    private static int ParseOrdinal(string word) => word.ToLowerInvariant() switch
    {
        "1st" or "first"   => 1,
        "2nd" or "second"  => 2,
        "3rd" or "third"   => 3,
        "4th" or "fourth"  => 4,
        "5th" or "fifth"   => 5,
        "6th" or "sixth"   => 6,
        "7th" or "seventh" => 7,
        "8th" or "eighth"  => 8,
        "9th" or "ninth"   => 9,
        _ => 0
    };
}
