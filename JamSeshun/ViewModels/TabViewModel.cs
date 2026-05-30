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
    public string Song => _tab?.Song ?? string.Empty;
    public string TuningDisplay => _tab is { Tuning: { Length: > 0 } t }
        ? (_tab.Capo > 0 ? $"{t}  ·  Capo: {_tab.Capo}" : t)
        : string.Empty;
    public bool HasTuning => !string.IsNullOrEmpty(_tab?.Tuning);
    public IReadOnlyList<Chord> Chords => ParseChords(_tab?.Content);
    public bool HasChords => Chords.Count > 0;
    public IReadOnlyList<TabLine> Lines => ParseLines(_tab?.Content);

    private static readonly Regex ChordRegex =
        new(@"^[A-G][#b]?[a-zA-Z0-9]*(\/[A-G][#b]?)?$", RegexOptions.Compiled);

    private static IReadOnlyList<Chord> ParseChords(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var chords = new List<Chord>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chordTokens = tokens.Where(t => ChordRegex.IsMatch(t)).ToList();
            if (tokens.Length > 0 && chordTokens.Count > 0 &&
                (double)chordTokens.Count / tokens.Length >= 0.5)
            {
                foreach (var t in chordTokens)
                    if (seen.Add(t))
                        chords.Add(new Chord(t));
            }
        }
        return chords;
    }

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
                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var chordCount = tokens.Count(t => ChordRegex.IsMatch(t));
                kind = tokens.Length > 0 && chordCount > 0 && (double)chordCount / tokens.Length >= 0.5
                    ? TabLineKind.Chord : TabLineKind.Lyric;
            }
            lines.Add(new TabLine(kind, line));
        }
        return lines;
    }
}
