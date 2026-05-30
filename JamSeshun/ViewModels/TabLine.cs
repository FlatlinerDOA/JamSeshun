using Avalonia.Media;

namespace JamSeshun.ViewModels;

public enum TabLineKind { Blank, Section, Chord, TabString, Lyric }

public record TabLine(TabLineKind Kind, string Text)
{
    private static readonly IBrush ChordBrush   = new SolidColorBrush(Color.Parse("#7fc0f4"));
    private static readonly IBrush TabBrush     = new SolidColorBrush(Color.Parse("#4ade80"));
    private static readonly IBrush SectionBrush = new SolidColorBrush(Color.Parse("#f1f5f9"));
    private static readonly IBrush LyricBrush   = new SolidColorBrush(Color.Parse("#d1d5db"));

    public IBrush ForegroundBrush => Kind switch
    {
        TabLineKind.Chord     => ChordBrush,
        TabLineKind.TabString => TabBrush,
        TabLineKind.Section   => SectionBrush,
        _                     => LyricBrush,
    };

    public FontWeight FontWeight => Kind == TabLineKind.Section ? FontWeight.SemiBold : FontWeight.Normal;
    public double MinHeight => Kind == TabLineKind.Blank ? 10 : 0;
}
