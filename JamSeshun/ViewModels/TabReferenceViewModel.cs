namespace JamSeshun.ViewModels;

public class TabReferenceViewModel : ViewModelBase
{
    public TabReferenceViewModel() : this(Guid.Empty, "Lorem Ipsum", "Dolor Set") { }

    public TabReferenceViewModel(Guid id, string artist, string song)
    {
        this.Id = id;
        this.Artist = artist;
        this.Song = song;
    }

    public Guid Id { get; }
    public string Artist { get; }
    public string Song { get; }
}
