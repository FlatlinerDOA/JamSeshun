using System.Text.RegularExpressions;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

public class TabViewModel : ViewModelBase
{
    private readonly TabLibraryService? _library;

    public TabViewModel() { }

    public TabViewModel(SavedTab tab)
    {
        _tab = tab;
    }

    public TabViewModel(TabLibraryService? library)
    {
        _library = library;
    }

    public Guid? Id { get; set; }

    private SavedTab? _tab;
    public SavedTab? Tab
    {
        get => _tab;
        set
        {
            if (_tab == value)
            {
                return;
            }
            _tab = value;
            OnPropertyChanged(nameof(Tab));
            OnPropertyChanged(nameof(Artist));
            OnPropertyChanged(nameof(Song));
            OnPropertyChanged(nameof(TuningDisplay));
            OnPropertyChanged(nameof(HasTuning));
            OnPropertyChanged(nameof(HasParsableTuning));
            OnPropertyChanged(nameof(HasTuningDisplayOnly));
            OnPropertyChanged(nameof(Chords));
            OnPropertyChanged(nameof(HasChords));
            OnPropertyChanged(nameof(Lines));
        }
    }

    public string Artist => _tab?.Artist ?? string.Empty;
    public string Song   => _tab?.Song   ?? string.Empty;

    public string TuningDisplay
    {
        get
        {
            if (_tab == null)
            {
                return string.Empty;
            }
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(_tab.Tuning))
            {
                parts.Add(_tab.Tuning);
            }
            if (_tab.Capo > 0)
            {
                parts.Add($"Capo {_tab.Capo}");
            }
            return string.Join("  ·  ", parts);
        }
    }

    public bool HasTuning => !string.IsNullOrEmpty(TuningDisplay);
    public bool HasParsableTuning => GuitarTuning.TryParse(_tab?.Tuning) != null;
    public bool HasTuningDisplayOnly => HasTuning && !HasParsableTuning;
    public IReadOnlyList<Chord> Chords => ParseChords(_tab?.Content);
    public bool HasChords => Chords.Count > 0;
    public IReadOnlyList<TabLine> Lines => ParseLines(_tab?.Content);

    // ── Chord name regex (used for both chord-line detection and inline scanning) ──

    // Matches a valid chord token: root (+ accidental), optional quality / extension,
    // optional slash bass. Deliberately strict so ordinary words that merely start with
    // A–G (e.g. "Chorus", "Come", "And", "Breathe") are NOT mistaken for chords.
    // Keyword qualities are matched case-insensitively (real tabs use "maj"/"Maj"/"M",
    // e.g. "AmMaj7"); the root note stays case-sensitive so lower-case words are rejected.
    private static readonly Regex ChordRegex = new(
        @"^[A-G][#b]?" +                                                  // root + accidental
        @"(?:(?i:maj|min|aug|dim|sus|add)|m|M|\+|°)?" +                   // quality
        @"[0-9]*" +                                                       // extension number(s)
        @"(?:(?:(?i:maj|min|sus|add|aug|dim)|m|M|b|#|\+|°)[0-9]*)*" +     // alterations: sus4, add9, b5…
        @"(?:/[A-G][#b]?)?$",                                             // optional slash bass
        RegexOptions.Compiled);

    // Header / metadata lines ("Tuning: D A D G B E", "Key: F#", "Standard Tuning: …",
    // "Capo: 2") contain note-like tokens that must not be scanned for chords.
    private static readonly Regex MetadataLineRegex = new(
        @"^\s*(?:\w+\s+)*(?:tuning|key|capo)\s*:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches a single-line chord definition. The name and frets are separated either by
    // a colon ("Am:[x02210]", "Am: [x02210]") or by whitespace ("Am  x02210", "Am x-0-2-2-1-0").
    // Capture groups: name, frets
    private static readonly Regex ChordDefRegex = new(
        @"^(?<name>[A-G][#b]?[\w/#]*)(?::\s*|\s+)(?<frets>\[?[xX0-9](?:[-\s]?[xX0-9]){5}\]?)",
        RegexOptions.Compiled);

    // Matches one row of a vertical chord diagram: a string letter, optional separator, a fret.
    //   "e-x"  "b-3"  "g-2"  "d-0"  "a-x"  "e-12"
    private static readonly Regex DiagramRowRegex = new(
        @"^(?<string>[eEbBgGdDaA])\s*[-:|]?\s*(?<fret>[xX]|\d+)$",
        RegexOptions.Compiled);

    // ── Chord parsing ────────────────────────────────────────────────────────────

    private static IReadOnlyList<Chord> ParseChords(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        // Build a frets lookup from the header Chords: section (may be empty).
        var defs = ParseChordDefinitions(text);

        var seen   = new HashSet<string>(StringComparer.Ordinal);
        var chords = new List<Chord>();

        // Emit defined chords first (content defs beat the library).
        foreach (var (name, frets) in defs)
            if (seen.Add(name))
            {
                chords.Add(new Chord(name, frets));
            }

        // Scan body lines for any chord names not already emitted.
        bool inChordSection = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.Trim().Equals("Chords:", StringComparison.OrdinalIgnoreCase))
            { inChordSection = true; continue; }

            if (inChordSection)
            {
                // Blank line or next section header ends the chord block.
                if (string.IsNullOrWhiteSpace(line) || (line.TrimStart().StartsWith('[') && line.TrimEnd().EndsWith(']')))
                {
                    inChordSection = false;
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            if (MetadataLineRegex.IsMatch(line))
            {
                continue;
            }
            var tokens     = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chordNames = tokens.Where(t => ChordRegex.IsMatch(t)).ToList();

            if (tokens.Length > 0 && chordNames.Count > 0 &&
                (double)chordNames.Count / tokens.Length >= 0.5)
            {
                foreach (var t in chordNames)
                    if (seen.Add(t))
                    {
                        chords.Add(new Chord(t, defs.GetValueOrDefault(t) ?? ChordLibrary.Lookup(t)));
                    }
            }
        }

        return chords;
    }

    private static Dictionary<string, int[]> ParseChordDefinitions(string text)
    {
        var result = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var lines  = text.Split('\n');
        int limit  = Math.Min(lines.Length, 80);

        // Chord definitions live in the header region and come in several shapes:
        //   • inside a "Chords:" / "[Chords]" block, or as a standalone block, written as
        //     single lines ("Am: [x02210]", "Am  x02210");
        //   • vertical diagrams (a lone chord name followed by six string rows).
        // The frets pattern is strict (six valid fret tokens), so scanning every header
        // line — rather than only a labelled section — does not pick up false positives.
        for (int i = 0; i < limit; i++)
        {
            if (TryParseVerticalDiagram(lines, i, out var diagName, out var diagFrets))
            {
                result.TryAdd(diagName, diagFrets);
                i += 6; // skip the six consumed string rows
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
    /// Parses a vertical chord diagram: a lone chord name followed by six string rows, e.g.
    /// <c>D/F#</c> then <c>e-x  b-3  g-2  d-0  a-x  e-2</c>. Rows may be ordered high→low
    /// (e b g d a e) or low→high (e a d g b e); both map to the [E A D G B e] fret array.
    /// </summary>
    private static bool TryParseVerticalDiagram(string[] lines, int index, out string name, out int[] frets)
    {
        name  = string.Empty;
        frets = [];

        var header = lines[index].TrimEnd('\r').Trim();
        if (!ChordRegex.IsMatch(header))
        {
            return false; // line must be a single chord name
        }
        if (index + 6 >= lines.Length)
        {
            return false; // need six following rows
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

        // Map each row to its index in the [E A D G B e] array based on the string ordering.
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
            return false; // unrecognised string ordering — don't guess
        }

        var result = new int[6];
        for (int r = 0; r < 6; r++)
            result[order[r]] = values[r];

        name  = header;
        frets = result;
        return true;
    }

    /// <summary>Parses a fret string like "[xx3210]", "x-0-2-2-1-0", or "x 0 2 2 1 0".</summary>
    private static int[]? TryParseFrets(string raw)
    {
        var cleaned = raw.Trim('[', ']', ' ');

        // Compact 6-char format with no separators: "xx3210"
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

        // Separated format: "x-0-2-2-1-0" or "x 0 2 2 1 0"
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

    // ── Line parsing ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<TabLine> ParseLines(string? text)
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

    public void ReloadTab()
    {
        if (Id == null || _library == null)
        {
            return;
        }
        var savedTab = _library.Get(Id.Value);
        if (savedTab != null)
        {
            Tab = savedTab;
        }
    }
}
