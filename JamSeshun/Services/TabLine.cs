using Avalonia.Media;

namespace JamSeshun.Services;

public enum TabLineKind { Blank, Section, Chord, TabString, Lyric }

public record TabLine(TabLineKind Kind, string Text)
{
    public static readonly IBrush ChordBrush   = new SolidColorBrush(Color.Parse("#7fc0f4"));
    public static readonly IBrush TabBrush     = new SolidColorBrush(Color.Parse("#4ade80"));
    public static readonly IBrush SectionBrush = new SolidColorBrush(Color.Parse("#f1f5f9"));
    public static readonly IBrush LyricBrush   = new SolidColorBrush(Color.Parse("#d1d5db"));

    public IBrush ForegroundBrush => this.Kind switch
    {
        TabLineKind.Chord     => ChordBrush,
        TabLineKind.TabString => TabBrush,
        TabLineKind.Section   => SectionBrush,
        _                     => LyricBrush,
    };

    public FontWeight FontWeight => this.Kind == TabLineKind.Section ? FontWeight.SemiBold : FontWeight.Normal;
    public double MinHeight => this.Kind == TabLineKind.Blank ? 10 : 0;
}
