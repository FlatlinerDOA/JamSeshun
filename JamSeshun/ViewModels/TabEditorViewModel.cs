using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public class TabEditorViewModel : ViewModelBase
{
    private readonly TabLibraryService? _library;
    private Guid? _editingId;
    private string artist = string.Empty;
    private string song = string.Empty;
    private string content = string.Empty;
    private string tuning = string.Empty;
    private int capo;
    private string savedMessage = string.Empty;

    public TabEditorViewModel()
    {
        SaveCommand = new RelayCommand(Save, CanSave);
        ClearCommand = new RelayCommand(Clear);
    }

    public TabEditorViewModel(TabLibraryService library) : this()
    {
        _library = library;
    }

    public string Artist
    {
        get => artist;
        set { SetProperty(ref artist, value); SaveCommand.NotifyCanExecuteChanged(); SavedMessage = string.Empty; }
    }

    public string Song
    {
        get => song;
        set { SetProperty(ref song, value); SaveCommand.NotifyCanExecuteChanged(); SavedMessage = string.Empty; }
    }

    public string Content
    {
        get => content;
        set { SetProperty(ref content, value); SavedMessage = string.Empty; }
    }

    public string Tuning
    {
        get => tuning;
        set => SetProperty(ref tuning, value);
    }

    public int Capo
    {
        get => capo;
        set => SetProperty(ref capo, value);
    }

    public string SavedMessage
    {
        get => savedMessage;
        private set { SetProperty(ref savedMessage, value); OnPropertyChanged(nameof(HasSavedMessage)); }
    }

    public bool HasSavedMessage => !string.IsNullOrEmpty(savedMessage);

    public RelayCommand SaveCommand { get; }
    public RelayCommand ClearCommand { get; }

    /// <summary>
    /// Imports a batch of files parsed by WikiTabParser. Returns the number successfully imported.
    /// </summary>
    public int ImportFiles(IEnumerable<(string FileName, string Content)> files)
    {
        if (_library == null) return 0;
        var count = 0;
        foreach (var (fileName, content) in files)
        {
            var tab = WikiTabParser.Parse(fileName, content);
            if (tab == null) continue;
            _library.Save(Guid.NewGuid(), $"{tab.Artist} - {tab.Song}", tab);
            count++;
        }
        if (count > 0)
            SavedMessage = $"Imported {count} tab{(count == 1 ? "" : "s")}";
        return count;
    }

    public void LoadForEdit(Guid id, SavedTab tab)
    {
        _editingId = id;
        Artist = tab.Artist;
        Song = tab.Song;
        Content = tab.Content;
        Tuning = tab.Tuning;
        Capo = tab.Capo;
        SavedMessage = string.Empty;
    }

    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(song);

    private void Save()
    {
        if (_library == null) return;
        var tab = new SavedTab(artist.Trim(), song.Trim(), content, tuning.Trim(), capo, DateTimeOffset.Now);
        _editingId ??= Guid.NewGuid();
        _library.Save(_editingId.Value, $"{tab.Artist} - {tab.Song}", tab);
        SavedMessage = "Saved!";
    }

    private void Clear()
    {
        Artist = string.Empty;
        Song = string.Empty;
        Content = string.Empty;
        Tuning = string.Empty;
        Capo = 0;
        SavedMessage = string.Empty;
        _editingId = null;
    }
}
