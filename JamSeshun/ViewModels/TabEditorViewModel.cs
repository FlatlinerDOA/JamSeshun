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
    private bool isImporting;
    private int importCurrent;
    private int importTotal;
    private int importSaved;

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

    public bool IsImporting
    {
        get => isImporting;
        private set
        {
            SetProperty(ref isImporting, value);
            OnPropertyChanged(nameof(CanInteract));
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    public int ImportCurrent
    {
        get => importCurrent;
        private set { SetProperty(ref importCurrent, value); OnPropertyChanged(nameof(ImportProgressText)); }
    }

    public int ImportTotal
    {
        get => importTotal;
        private set { SetProperty(ref importTotal, value); OnPropertyChanged(nameof(ImportProgressText)); }
    }

    public string ImportProgressText => $"{importCurrent} / {importTotal}";

    public bool CanInteract => !isImporting;

    public RelayCommand SaveCommand { get; }
    public RelayCommand ClearCommand { get; }

    public void BeginImport(int total)
    {
        ImportTotal = total;
        ImportCurrent = 0;
        importSaved = 0;
        SavedMessage = string.Empty;
        IsImporting = true;
    }

    /// <summary>
    /// Parse and save one file on a background thread.
    /// Returns false if the file was skipped (duplicate or unrecognised filename).
    /// </summary>
    public async Task<bool> ImportOneAsync(string fileName, string content)
    {
        if (_library == null) return false;

        var saved = await Task.Run(() =>
        {
            var tab = WikiTabParser.Parse(fileName, content);
            if (tab == null) return false;

            var version = WikiTabParser.ParseVersion(fileName);
            var name    = WikiTabParser.StoreKey(tab.Artist, tab.Song, version);

            if (_library.NameExists(name)) return false;

            _library.Save(Guid.NewGuid(), name, tab);
            return true;
        });

        ImportCurrent++;
        if (saved) importSaved++;
        return saved;
    }

    public void EndImport()
    {
        IsImporting = false;
        var skipped = importCurrent - importSaved;
        SavedMessage = importSaved > 0
            ? skipped > 0
                ? $"Imported {importSaved}, skipped {skipped} duplicate{(skipped == 1 ? "" : "s")}"
                : $"Imported {importSaved} tab{(importSaved == 1 ? "" : "s")}"
            : importCurrent > 0
                ? "All files already imported"
                : "No valid tabs found";
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
        !isImporting && !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(song);

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
