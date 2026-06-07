using System.Text.RegularExpressions;

namespace JamSeshun.Services;

public static class WikiTabParser
{
    internal static readonly Regex FilenameRegex = new(
        @"^(?<artist>.+?) - (?<song>.+?) V(?<version>\d+)\.(?<type>[^.]+)\.txt$",
        RegexOptions.Compiled);

    // Matches: Capo: 1 | Capo: 1st fret | Capo: (None) | Capo 2 | CAPO 2 | Capo on first fret
    internal static readonly Regex CapoRegex = new(
        @"^\s*capo\s*:?\s*(?:\((?<none>none)\)|on\s+(?:the\s+)?(?<ordword>\w+)\s+fret|(?<ordword2>\w+)\s+fret|(?<num>\d+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches note names like "E A D G B E" or "Eb Ab Db Gb Bb Eb".
    internal static readonly Regex TuningRegex = new(
        @"^\s*(?:(?<prefix>\w+)\s+)?tuning\s*:\s*(?<tuning>(?:[A-G][#b]?\s+)*[A-G][#b]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches a valid chord token: root (+ accidental), optional quality / extension, optional slash bass.
    // Deliberately strict so ordinary words starting with A–G (e.g. "Chorus", "Come") are NOT mistaken for chords.
    internal static readonly Regex ChordRegex = new(
        @"^[A-G][#b]?" +
        @"(?:(?i:maj|min|aug|dim|sus|add)|m|M|\+|°)?" +
        @"[0-9]*" +
        @"(?:(?:(?i:maj|min|sus|add|aug|dim)|m|M|b|#|\+|°)[0-9]*)*" +
        @"(?:/[A-G][#b]?)?$",
        RegexOptions.Compiled);

    // Header/metadata lines that must not be scanned for chord tokens.
    internal static readonly Regex MetadataLineRegex = new(
        @"^\s*(?:\w+\s+)*(?:tuning|key|capo)\s*:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Single-line chord definition: "Am:[x02210]" or "Am  x02210".
    internal static readonly Regex ChordDefRegex = new(
        @"^(?<name>[A-G][#b]?[\w/#]*)(?::\s*|\s+)(?<frets>\[?[xX0-9](?:[-\s]?[xX0-9]){5}\]?)",
        RegexOptions.Compiled);

    // One row of a vertical chord diagram: "e-x", "b-3", "g-2".
    internal static readonly Regex DiagramRowRegex = new(
        @"^(?<string>[eEbBgGdDaA])\s*[-:|]?\s*(?<fret>[xX]|\d+)$",
        RegexOptions.Compiled);

    // ── File / filename parsing ──────────────────────────────────────────────

    /// <summary>Parses a WikiTab .txt file into a SavedTab. Returns null if the filename doesn't match.</summary>
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
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var dotIdx = stem.LastIndexOf('.');
            if (dotIdx > 0)
            {
                stem = stem[..dotIdx];
            }

            var dash = stem.IndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0)
            {
                return null;
            }

            artist = stem[..dash].Trim();
            song   = stem[(dash + 3)..].Trim();
            if (string.IsNullOrEmpty(song))
            {
                return null;
            }
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

    /// <summary>Returns the library store key: "Artist - Song" for V1, "Artist - Song V2" for higher versions.</summary>
    public static string StoreKey(string artist, string song, int version) =>
        version > 1 ? $"{artist} - {song} V{version}" : $"{artist} - {song}";

    /// <summary>Parses all matching .txt files in a directory.</summary>
    public static IEnumerable<SavedTab> ParseDirectory(string directoryPath) =>
        Directory.EnumerateFiles(directoryPath, "*.txt")
                 .Select(ParseFile)
                 .OfType<SavedTab>();

    // ── Tab content parsing ──────────────────────────────────────────────────

    public static IReadOnlyList<Chord> ParseChords(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var defs   = ParseChordDefinitions(text);
        var seen   = new HashSet<string>(StringComparer.Ordinal);
        var chords = new List<Chord>();

        foreach (var (name, frets) in defs)
        {
            if (seen.Add(name))
            {
                chords.Add(new Chord(name, frets));
            }
        }

        bool inChordSection = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.Trim().Equals("Chords:", StringComparison.OrdinalIgnoreCase))
            {
                inChordSection = true;
                continue;
            }

            if (inChordSection)
            {
                if (string.IsNullOrWhiteSpace(line) || (line.TrimStart().StartsWith('[') && line.TrimEnd().EndsWith(']')))
                {
                    inChordSection = false;
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || MetadataLineRegex.IsMatch(line))
            {
                continue;
            }

            var tokens     = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chordNames = tokens.Where(t => ChordRegex.IsMatch(t)).ToList();

            if (tokens.Length > 0 && chordNames.Count > 0 && (double)chordNames.Count / tokens.Length >= 0.5)
            {
                foreach (var t in chordNames)
                {
                    if (seen.Add(t))
                    {
                        chords.Add(new Chord(t, defs.GetValueOrDefault(t) ?? ChordLibrary.Lookup(t)));
                    }
                }
            }
        }

        return chords;
    }

    public static Dictionary<string, int[]> ParseChordDefinitions(string text)
    {
        var result = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var lines  = text.Split('\n');
        int limit  = Math.Min(lines.Length, 80);

        for (int i = 0; i < limit; i++)
        {
            if (TryParseVerticalDiagram(lines, i, out var diagName, out var diagFrets))
            {
                result.TryAdd(diagName, diagFrets);
                i += 6;
                continue;
            }

            var line = lines[i].TrimEnd('\r').Trim();
            var m    = ChordDefRegex.Match(line);
            if (!m.Success)
            {
                continue;
            }

            var frets = TryParseFrets(m.Groups["frets"].Value);
            if (frets != null)
            {
                result.TryAdd(m.Groups["name"].Value, frets);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a vertical chord diagram: a lone chord name followed by six string rows.
    /// Rows may be ordered high→low (e b g d a e) or low→high (e a d g b e).
    /// </summary>
    public static bool TryParseVerticalDiagram(string[] lines, int index, out string name, out int[] frets)
    {
        name  = string.Empty;
        frets = [];

        var header = lines[index].TrimEnd('\r').Trim();
        if (!ChordRegex.IsMatch(header) || index + 6 >= lines.Length)
        {
            return false;
        }

        var letters = new char[6];
        var values  = new int[6];
        for (int r = 0; r < 6; r++)
        {
            var m = DiagramRowRegex.Match(lines[index + 1 + r].TrimEnd('\r').Trim());
            if (!m.Success)
            {
                return false;
            }
            letters[r] = char.ToLowerInvariant(m.Groups["string"].Value[0]);
            var f = m.Groups["fret"].Value;
            values[r] = f is "x" or "X" ? -1 : int.Parse(f);
        }

        int[] order;
        if (letters.SequenceEqual(['e', 'b', 'g', 'd', 'a', 'e']))
        {
            order = [5, 4, 3, 2, 1, 0];
        }
        else if (letters.SequenceEqual(['e', 'a', 'd', 'g', 'b', 'e']))
        {
            order = [0, 1, 2, 3, 4, 5];
        }
        else
        {
            return false;
        }

        var result = new int[6];
        for (int r = 0; r < 6; r++)
        {
            result[order[r]] = values[r];
        }

        name  = header;
        frets = result;
        return true;
    }

    /// <summary>Parses a fret string like "[xx3210]", "x-0-2-2-1-0", or "x 0 2 2 1 0".</summary>
    public static int[]? TryParseFrets(string raw)
    {
        var cleaned = raw.Trim('[', ']', ' ');

        if (cleaned.Length == 6 && !cleaned.Contains('-') && !cleaned.Contains(' '))
        {
            var arr = new int[6];
            for (int i = 0; i < 6; i++)
            {
                var c = cleaned[i];
                if (c is 'x' or 'X')
                {
                    arr[i] = -1;
                }
                else if (c is >= '0' and <= '9')
                {
                    arr[i] = c - '0';
                }
                else
                {
                    return null;
                }
            }
            return arr;
        }

        var parts = cleaned.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6)
        {
            return null;
        }

        var result = new int[6];
        for (int i = 0; i < 6; i++)
        {
            if (parts[i].Equals("x", StringComparison.OrdinalIgnoreCase))
            {
                result[i] = -1;
            }
            else if (!int.TryParse(parts[i], out result[i]))
            {
                return null;
            }
        }
        return result;
    }

    public static IReadOnlyList<TabLine> ParseLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }
        var lines = new List<TabLine>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            TabLineKind kind;
            if (string.IsNullOrWhiteSpace(line))
            {
                kind = TabLineKind.Blank;
            }
            else if (line.Length > 1 && line[1] == '|')
            {
                kind = TabLineKind.TabString;
            }
            else if (line.TrimEnd().EndsWith(':') && line.Length < 25 && !line.Contains('|'))
            {
                kind = TabLineKind.Section;
            }
            else if (MetadataLineRegex.IsMatch(line))
            {
                kind = TabLineKind.Lyric;
            }
            else
            {
                var tokens     = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var chordCount = tokens.Count(t => ChordRegex.IsMatch(t));
                kind = tokens.Length > 0 && chordCount > 0 && (double)chordCount / tokens.Length >= 0.5
                    ? TabLineKind.Chord : TabLineKind.Lyric;
            }
            lines.Add(new TabLine(kind, line));
        }
        return lines;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static (string Tuning, int Capo) ParseMetadata(string content)
    {
        var tuning         = string.Empty;
        var tuningPriority = int.MaxValue;
        var capo           = 0;

        foreach (var raw in content.Split('\n').Take(25))
        {
            var line = raw.TrimEnd('\r');

            var capoMatch = CapoRegex.Match(line);
            if (capoMatch.Success)
            {
                if (capoMatch.Groups["none"].Success)
                {
                    capo = 0;
                }
                else if (capoMatch.Groups["num"].Success)
                {
                    int.TryParse(capoMatch.Groups["num"].Value, out capo);
                }
                else if (capoMatch.Groups["ordword"].Success)
                {
                    capo = ParseOrdinal(capoMatch.Groups["ordword"].Value);
                }
                else if (capoMatch.Groups["ordword2"].Success)
                {
                    capo = ParseOrdinal(capoMatch.Groups["ordword2"].Value);
                }
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

    private static int TuningPriority(string prefix) => prefix.ToLowerInvariant() switch
    {
        "studio"   => 0,
        "standard" => 1,
        ""         => 1,
        "live"     => 2,
        _          => 1,
    };

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
