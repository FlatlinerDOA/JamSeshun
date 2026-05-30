using System.Text.RegularExpressions;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public class TabViewModel : ViewModelBase
{
    public TabViewModel() { }

    public TabViewModel(SavedTab tab)
    {
        _tab = tab;
    }

    public Guid? Id { get; set; }

    private SavedTab? _tab;
    public SavedTab? Tab
    {
        get => _tab;
        set
        {
            if (_tab == value) return;
            _tab = value;
            OnPropertyChanged(nameof(Tab));
            OnPropertyChanged(nameof(Artist));
            OnPropertyChanged(nameof(Song));
            OnPropertyChanged(nameof(TuningDisplay));
            OnPropertyChanged(nameof(HasTuning));
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
            if (_tab == null) return string.Empty;
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(_tab.Tuning))
                parts.Add(_tab.Tuning);
            if (_tab.Capo > 0)
                parts.Add($"Capo {_tab.Capo}");
            return string.Join("  ·  ", parts);
        }
    }

    public bool HasTuning => !string.IsNullOrEmpty(TuningDisplay);
    public IReadOnlyList<Chord> Chords => ParseChords(_tab?.Content);
    public bool HasChords => Chords.Count > 0;
    public IReadOnlyList<TabLine> Lines => ParseLines(_tab?.Content);

    // ── Chord name regex (used for both chord-line detection and inline scanning) ──

    private static readonly Regex ChordRegex = new(
        @"^[A-G][#b]?[a-zA-Z0-9]*(\/[A-G][#b]?)?$", RegexOptions.Compiled);

    // Matches a chord definition line: "Am: [x02210]", "Am:[x02210]", "Am  x-0-2-2-1-0"
    // Capture groups: name, frets
    private static readonly Regex ChordDefRegex = new(
        @"^(?<name>[A-G][#b]?[\w/]*)\s*:?\s+(?<frets>\[?[xX0-9](?:[-\s]?[xX0-9]){5}\]?)",
        RegexOptions.Compiled);

    // ── Chord parsing ────────────────────────────────────────────────────────────

    private static IReadOnlyList<Chord> ParseChords(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        // Build a frets lookup from the header Chords: section (may be empty).
        var defs = ParseChordDefinitions(text);

        var seen   = new HashSet<string>(StringComparer.Ordinal);
        var chords = new List<Chord>();

        // Emit defined chords first (content defs beat the library).
        foreach (var (name, frets) in defs)
            if (seen.Add(name))
                chords.Add(new Chord(name, frets));

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
                    inChordSection = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;
            var tokens     = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chordNames = tokens.Where(t => ChordRegex.IsMatch(t)).ToList();

            if (tokens.Length > 0 && chordNames.Count > 0 &&
                (double)chordNames.Count / tokens.Length >= 0.5)
            {
                foreach (var t in chordNames)
                    if (seen.Add(t))
                        chords.Add(new Chord(t, defs.GetValueOrDefault(t) ?? ChordLibrary.Lookup(t)));
            }
        }

        return chords;
    }

    private static Dictionary<string, int[]> ParseChordDefinitions(string text)
    {
        var result         = new Dictionary<string, int[]>(StringComparer.Ordinal);
        bool inChordSection = false;

        foreach (var raw in text.Split('\n').Take(80))
        {
            var line = raw.TrimEnd('\r').Trim();

            if (line.Equals("Chords:", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("[Chords]", StringComparison.OrdinalIgnoreCase))
            { inChordSection = true; continue; }

            if (!inChordSection) continue;

            // A bracketed section header or blank line ends the chord block.
            if (string.IsNullOrWhiteSpace(line) ||
                (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2))
                break;

            var m = ChordDefRegex.Match(line);
            if (!m.Success) continue;

            var frets = TryParseFrets(m.Groups["frets"].Value);
            if (frets != null)
                result[m.Groups["name"].Value] = frets;
        }

        return result;
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
                if (c is 'x' or 'X')          arr[i] = -1;
                else if (c is >= '0' and <= '9') arr[i] = c - '0';
                else return null;
            }
            return arr;
        }

        // Separated format: "x-0-2-2-1-0" or "x 0 2 2 1 0"
        var parts = cleaned.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6) return null;

        var result = new int[6];
        for (int i = 0; i < 6; i++)
        {
            if (parts[i].Equals("x", StringComparison.OrdinalIgnoreCase))
                result[i] = -1;
            else if (!int.TryParse(parts[i], out result[i]))
                return null;
        }
        return result;
    }

    // ── Line parsing ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<TabLine> ParseLines(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var lines = new List<TabLine>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            TabLineKind kind;
            if (string.IsNullOrWhiteSpace(line))
                kind = TabLineKind.Blank;
            else if (line.Length > 1 && line[1] == '|')
                kind = TabLineKind.TabString;
            else if (line.TrimEnd().EndsWith(':') && line.Length < 25 && !line.Contains('|'))
                kind = TabLineKind.Section;
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
}
