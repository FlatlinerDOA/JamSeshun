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

    // Matches note names like "E A D G B E" or "Eb Ab Db Gb Bb Eb".
    // Captures an optional word prefix ("Studio", "Live", "Standard", …) for priority ranking.
    private static readonly Regex TuningRegex = new(
        @"^\s*(?:(?<prefix>\w+)\s+)?tuning\s*:\s*(?<tuning>(?:[A-G][#b]?\s+)*[A-G][#b]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Lower number = higher priority. "Studio Tuning:" wins over a bare "Tuning:".
    private static int TuningPriority(string prefix) => prefix.ToLowerInvariant() switch
    {
        "studio"   => 0,
        "standard" => 1,
        ""         => 1,
        "live"     => 2,
        _          => 1,
    };

    /// <summary>Parses a WikiTab .txt file into a SavedTab. Returns null if the filename doesn't match the expected pattern.</summary>
    public static SavedTab? ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(Path.GetFileName(filePath), content);
    }

    /// <summary>Parses a filename + raw content into a SavedTab.</summary>
    public static SavedTab? Parse(string fileName, string content)
    {
        string artist, song;

        var m = FilenameRegex.Match(fileName);
        if (m.Success)
        {
            artist = m.Groups["artist"].Value.Trim();
            song   = m.Groups["song"].Value.Trim();
        }
        else
        {
            // Fallback: accept "Artist - Song.txt" (no version/type suffix).
            // Strip extension and any trailing ".something" type suffix, then split on " - ".
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var dotIdx = stem.LastIndexOf('.');
            if (dotIdx > 0) stem = stem[..dotIdx];

            var dash = stem.IndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0) return null;

            artist = stem[..dash].Trim();
            song   = stem[(dash + 3)..].Trim();
            if (string.IsNullOrEmpty(song)) return null;
        }

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
        var tuning         = string.Empty;
        var tuningPriority = int.MaxValue;
        var capo           = 0;

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
                var prefix   = tuningMatch.Groups["prefix"].Value.Trim();
                var priority = TuningPriority(prefix);
                if (priority < tuningPriority)
                {
                    tuning         = tuningMatch.Groups["tuning"].Value.Trim();
                    tuningPriority = priority;
                }
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
