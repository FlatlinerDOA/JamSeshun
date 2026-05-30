using System.Text.RegularExpressions;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public class TabViewModel : ViewModelBase
{
    public TabViewModel() { }

    public TabViewModel(Tab tab)
    {
        Tab = tab;
    }

    public Tab? Tab { get; }
    public string Artist => Tab?.Name.Artist ?? string.Empty;
    public string Song => Tab?.Name.Song ?? string.Empty;
    public string? WikiTab => Tab?.WikiTab;
    public GuitarTuning? Tuning => Tab?.Tuning;
    public string TuningDisplay => Tuning != null
        ? Tuning.Capo > 0 ? $"{Tuning.Name}  ·  Capo: {Tuning.Capo}" : Tuning.Name
        : string.Empty;
    public bool HasTuning => Tab?.Tuning != null && !string.IsNullOrEmpty(Tab.Tuning.Name);
    public IReadOnlyList<Chord> Chords => Tab?.Chords ?? [];
    public bool HasChords => Chords.Count > 0;

    public IReadOnlyList<TabLine> Lines => ParseLines(WikiTab);

    private static readonly Regex ChordRegex =
        new(@"^[A-G][#b]?[a-zA-Z0-9]*(\/[A-G][#b]?)?$", RegexOptions.Compiled);

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
