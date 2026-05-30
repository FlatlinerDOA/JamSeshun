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
        SaveCommand         = new RelayCommand(Save, CanSave);
        DeleteCommand       = new RelayCommand(() => IsConfirmingDelete = true, () => _editingId.HasValue);
        ConfirmDeleteCommand = new RelayCommand(Delete);
        CancelDeleteCommand  = new RelayCommand(() => IsConfirmingDelete = false);
        ClearCommand        = new RelayCommand(Clear);
        IncrementCapoCommand = new RelayCommand(() => { if (capo < 12) Capo = capo + 1; });
        DecrementCapoCommand = new RelayCommand(() => { if (capo > 0)  Capo = capo - 1; });
    }

    public TabEditorViewModel(TabLibraryService library) : this()
    {
        _library = library;
    }

    public string Title => _editingId.HasValue ? "Edit Tab" : "New Tab";

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
        set { SetProperty(ref capo, value); OnPropertyChanged(nameof(CapoLabel)); }
    }

    public string CapoLabel => capo > 0 ? $"Capo {capo}" : "Capo";

    public string SavedMessage
    {
        get => savedMessage;
        private set { SetProperty(ref savedMessage, value); OnPropertyChanged(nameof(HasSavedMessage)); }
    }

    public bool HasSavedMessage => !string.IsNullOrEmpty(savedMessage);

    public event Action? Saved;
    public event Action? Deleted;

    private bool isConfirmingDelete;
    public bool IsConfirmingDelete
    {
        get => isConfirmingDelete;
        private set => SetProperty(ref isConfirmingDelete, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }        // shows confirmation
    public RelayCommand ConfirmDeleteCommand { get; } // actually deletes
    public RelayCommand CancelDeleteCommand { get; }  // hides confirmation
    public RelayCommand ClearCommand { get; }
    public RelayCommand IncrementCapoCommand { get; }
    public RelayCommand DecrementCapoCommand { get; }

    public bool CanDelete => _editingId.HasValue;

    public void LoadForEdit(Guid id, SavedTab tab)
    {
        _editingId = id;
        Artist = tab.Artist;
        Song = tab.Song;
        Content = tab.Content;
        Tuning = tab.Tuning;
        Capo = tab.Capo;
        SavedMessage = string.Empty;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(song);

    private void Delete()
    {
        if (_library == null || _editingId == null) return;
        IsConfirmingDelete = false;
        _library.Delete(_editingId.Value);
        Deleted?.Invoke();
    }

    private void Save()
    {
        if (_library == null) return;
        var tab = new SavedTab(artist.Trim(), song.Trim(), content, tuning.Trim(), capo, DateTimeOffset.Now);
        _editingId ??= Guid.NewGuid();
        _library.Save(_editingId.Value, $"{tab.Artist} - {tab.Song}", tab);
        SavedMessage = "Saved!";
        Saved?.Invoke();
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
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
        DeleteCommand.NotifyCanExecuteChanged();
    }
}
