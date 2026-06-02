using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public partial class TabListViewModel : ViewModelBase
{
    private readonly TabLibraryService? _library;
    private string searchQuery = string.Empty;
    private TabReferenceViewModel? selectedTabReference;
    private bool isImporting;
    private int importCurrent;
    private int importTotal;

    public TabListViewModel()
    {
        SearchResults.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(HasNoResults));
        };
    }

    public TabListViewModel(TabLibraryService library) : this()
    {
        _library = library;
        _library.Changed += LoadAll;
        LoadAll();
    }

    private void LoadAll()
    {
        AllTabs.Clear();
        if (_library == null) return;
        foreach (var (id, name) in _library.GetAll().OrderBy(e => SongPart(e.Name)))
        {
            var parts = name.Split(" - ", 2);
            AllTabs.Add(new TabReferenceViewModel(id,
                parts.Length > 1 ? parts[0] : name,
                parts.Length > 1 ? parts[1] : string.Empty));
        }
    }

    public SavedTab? LoadTab(Guid id) => _library?.Get(id);

    public void DeleteTab(Guid id)
    {
        _library?.Delete(id);
        // LoadAll() fires via Changed event
    }

    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            if (SetProperty(ref searchQuery, value))
            {
                FilterResults();
                OnPropertyChanged(nameof(IsSearching));
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool IsSearching    => !string.IsNullOrWhiteSpace(searchQuery);
    public bool HasNoResults   => IsSearching && !HasSearchResults;

    public TabReferenceViewModel? SelectedTabReference
    {
        get => selectedTabReference;
        set => SetProperty(ref selectedTabReference, value);
    }

    public ObservableCollection<TabReferenceViewModel> AllTabs { get; } = new();
    public ObservableCollection<TabReferenceViewModel> SearchResults { get; } = new();
    public bool HasSearchResults => SearchResults.Count > 0;

    private void FilterResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(searchQuery) || _library == null) return;
        foreach (var (id, name) in _library.Search(searchQuery).OrderBy(e => SongPart(e.Name)))
        {
            var parts = name.Split(" - ", 2);
            SearchResults.Add(new TabReferenceViewModel(id,
                parts.Length > 1 ? parts[0] : name,
                parts.Length > 1 ? parts[1] : string.Empty));
        }
    }

    // ── Import ───────────────────────────────────────────────────────────────

    public bool IsImporting
    {
        get => isImporting;
        private set { SetProperty(ref isImporting, value); OnPropertyChanged(nameof(CanImport)); }
    }
    public bool CanImport => !isImporting;
    public int ImportCurrent { get => importCurrent; private set { SetProperty(ref importCurrent, value); OnPropertyChanged(nameof(ImportProgressText)); } }
    public int ImportTotal   { get => importTotal;   private set { SetProperty(ref importTotal,   value); OnPropertyChanged(nameof(ImportProgressText)); } }
    public string ImportProgressText => $"{importCurrent} / {importTotal}";
    public string ImportStatusMessage { get; private set; } = string.Empty;
    public bool HasImportStatus => !string.IsNullOrEmpty(ImportStatusMessage);

    public void BeginImport(int total)
    {
        ImportTotal = total;
        ImportCurrent = 0;
        ImportStatusMessage = string.Empty;
        OnPropertyChanged(nameof(ImportStatusMessage));
        OnPropertyChanged(nameof(HasImportStatus));
        IsImporting = true;
    }

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
        return saved;
    }

    public void EndImport(int saved)
    {
        IsImporting = false;
        var skipped = importCurrent - saved;
        ImportStatusMessage = saved > 0
            ? skipped > 0 ? $"Imported {saved}, skipped {skipped} duplicate{(skipped == 1 ? "" : "s")}"
                          : $"Imported {saved} tab{(saved == 1 ? "" : "s")}"
            : importCurrent > 0 ? "All files already imported"
                                : "No valid tabs found";
        OnPropertyChanged(nameof(ImportStatusMessage));
        OnPropertyChanged(nameof(HasImportStatus));
    }

    private static string SongPart(string name)
    {
        var idx = name.IndexOf(" - ", StringComparison.Ordinal);
        return idx >= 0 ? name[(idx + 3)..] : name;
    }
}
