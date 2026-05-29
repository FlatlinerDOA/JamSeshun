using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public class TabReferenceViewModel : ViewModelBase
{
    public TabReferenceViewModel()
        : this(new TabReference("Lorem Ipsum", "Dolor Set", 1, "Tab", 1, 0.9m, null)) { }

    public TabReferenceViewModel(TabReference tabReference)
    {
        TabReference = tabReference;
    }

    public TabReference TabReference { get; }
    public string Artist => TabReference.Artist;
    public string Song => TabReference.Song;
    public int Version => TabReference.Version;
    public int Votes => TabReference.Votes;
    public decimal Rating => TabReference.Rating;
    public string? Url => TabReference.Url;
    public string Summary => $"v{Version}  ·  {Votes:N0} votes";
}
