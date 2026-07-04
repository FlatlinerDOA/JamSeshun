namespace JamSeshun.ViewModels;

using Services;

public sealed class PerformanceLineViewModel : ViewModelBase
{
    public PerformanceLineViewModel(PerformanceLine? line)
    {
        this.Line = line;
    }

    public PerformanceLine? Line { get; }

    public IReadOnlyList<Chord> Chords => this.Line?.Chords ?? [];

    public string Lyric => this.Line?.LyricText ?? string.Empty;

    public bool HasLyric => !string.IsNullOrEmpty(this.Line?.LyricText);

    public string SectionLabel => this.Line?.SectionLabel ?? string.Empty;

    public bool HasSectionLabel => !string.IsNullOrEmpty(this.Line?.SectionLabel);
}
