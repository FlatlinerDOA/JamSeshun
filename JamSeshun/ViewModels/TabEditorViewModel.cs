using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public class TabEditorViewModel : ViewModelBase
{
    private readonly TabLibraryService? library;
    private readonly INavigationService? navigation;
    private Guid? editingId;
    private string artist = string.Empty;
    private string song = string.Empty;
    private string content = string.Empty;
    private string tuning = string.Empty;
    private int capo;
    private string savedMessage = string.Empty;

    public TabEditorViewModel()
    {
        this.BackCommand    = new AsyncRelayCommand(this.OnBack);
        this.SaveCommand    = new RelayCommand(this.Save, this.CanSave);
        this.DeleteCommand  = new RelayCommand(() => this.IsConfirmingDelete = true, () => this.editingId.HasValue);
        this.ConfirmDeleteCommand = new RelayCommand(this.Delete);
        this.CancelDeleteCommand  = new RelayCommand(() => this.IsConfirmingDelete = false);
        this.ClearCommand         = new RelayCommand(this.Clear);
        this.IncrementCapoCommand = new RelayCommand(() =>
        {
            if (this.capo < 12)
            {
                this.Capo = this.capo + 1;
            }
        });
        this.DecrementCapoCommand = new RelayCommand(() =>
        {
            if (this.capo > 0)
            {
                this.Capo = this.capo - 1;
            }
        });
    }

    public TabEditorViewModel(TabLibraryService library, INavigationService navigation) : this()
    {
        this.library = library;
        this.navigation = navigation;
    }

    public string Title => this.editingId.HasValue ? "Edit Tab" : "New Tab";

    public string Artist
    {
        get => this.artist;
        set
        {
            this.SetProperty(ref this.artist, value);
            this.SaveCommand.NotifyCanExecuteChanged();
            this.SavedMessage = string.Empty;
        }
    }

    public string Song
    {
        get => this.song;
        set
        {
            this.SetProperty(ref this.song, value);
            this.SaveCommand.NotifyCanExecuteChanged();
            this.SavedMessage = string.Empty;
        }
    }

    public string Content
    {
        get => this.content;
        set
        {
            this.SetProperty(ref this.content, value);
            this.SavedMessage = string.Empty;
        }
    }

    public string Tuning
    {
        get => this.tuning;
        set => this.SetProperty(ref this.tuning, value);
    }

    public int Capo
    {
        get => this.capo;
        set
        {
            this.SetProperty(ref this.capo, value);
            this.OnPropertyChanged(nameof(TabEditorViewModel.CapoLabel));
        }
    }

    public string CapoLabel => this.capo > 0 ? $"Capo {this.capo}" : "Capo";

    public string SavedMessage
    {
        get => this.savedMessage;
        private set
        {
            this.SetProperty(ref this.savedMessage, value);
            this.OnPropertyChanged(nameof(TabEditorViewModel.HasSavedMessage));
        }
    }

    public bool HasSavedMessage => !string.IsNullOrEmpty(this.savedMessage);

    private bool isConfirmingDelete;
    public bool IsConfirmingDelete
    {
        get => this.isConfirmingDelete;
        private set => this.SetProperty(ref this.isConfirmingDelete, value);
    }

    public AsyncRelayCommand BackCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand IncrementCapoCommand { get; }
    public RelayCommand DecrementCapoCommand { get; }

    public bool CanDelete => this.editingId.HasValue;

    public void LoadForEdit(Guid id, SavedTab tab)
    {
        this.editingId = id;
        this.Artist = tab.Artist;
        this.Song = tab.Song;
        this.Content = tab.Content;
        this.Tuning = tab.Tuning;
        this.Capo = tab.Capo;
        this.SavedMessage = string.Empty;
        this.OnPropertyChanged(nameof(TabEditorViewModel.Title));
        this.OnPropertyChanged(nameof(TabEditorViewModel.CanDelete));
        this.DeleteCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(this.artist) && !string.IsNullOrWhiteSpace(this.song);

    private void Delete()
    {
        if (this.library == null || this.editingId == null)
        {
            return;
        }
        this.IsConfirmingDelete = false;
        this.library.Delete(this.editingId.Value);
        _ = this.navigation?.PopToRootAsync();
    }

    private void Save()
    {
        if (this.library == null)
        {
            return;
        }
        var tab = new SavedTab(this.artist.Trim(), this.song.Trim(), this.content, this.tuning.Trim(), this.capo, DateTimeOffset.Now);
        this.editingId ??= Guid.NewGuid();
        this.library.Save(this.editingId.Value, $"{tab.Artist} - {tab.Song}", tab);
        this.SavedMessage = "Saved!";
        _ = this.navigation?.PopAsync();
    }

    private void Clear()
    {
        this.Artist = string.Empty;
        this.Song = string.Empty;
        this.Content = string.Empty;
        this.Tuning = string.Empty;
        this.Capo = 0;
        this.SavedMessage = string.Empty;
        this.editingId = null;
        this.OnPropertyChanged(nameof(TabEditorViewModel.Title));
        this.OnPropertyChanged(nameof(TabEditorViewModel.CanDelete));
        this.DeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task OnBack()
    {
        if (this.navigation != null)
        {
            await this.navigation.PopAsync();
        }
    }
}
