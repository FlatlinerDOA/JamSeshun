using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public partial class TabListViewModel : ViewModelBase, IDisposable
{
    private readonly TabLibraryService? library;
    private string searchQuery = string.Empty;
    private TabReferenceViewModel? selectedTabReference;
    private bool isImporting;
    private int importCurrent;
    private int importTotal;
    private readonly CompositeDisposable disposables = new();
    private CancellationTokenSource filterCts = new();

    public TabListViewModel()
    {
        this.disposables.Add(
            Observable.FromEventPattern(this.SearchResults, nameof(this.SearchResults.CollectionChanged))
                .Subscribe(_ =>
                {
                    this.OnPropertyChanged(nameof(TabListViewModel.HasSearchResults));
                    this.OnPropertyChanged(nameof(TabListViewModel.HasNoResults));
                })
        );
    }

    public TabListViewModel(TabLibraryService library) : this()
    {
        this.library = library;
        this.disposables.Add(
            this.library.Changed
                .SelectMany(_ => Observable.FromAsync(async ct =>
                    await Task.Run(() => this.library.GetAll().OrderBy(e => SongPart(e.Name)).ToList(), ct)))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(this.ApplyTabs)
        );
    }

    public async Task LoadAllAsync()
    {
        if (this.library == null)
        {
            return;
        }
        var tabs = await Task.Run(() => this.library.GetAll().OrderBy(e => SongPart(e.Name)).ToList());
        this.ApplyTabs(tabs);
    }

    private void ApplyTabs(List<(Guid Id, string Name)> tabs)
    {
        this.AllTabs.Clear();
        foreach (var (id, name) in tabs)
        {
            var parts = name.Split(" - ", 2);
            this.AllTabs.Add(new TabReferenceViewModel(id,
                parts.Length > 1 ? parts[0] : name,
                parts.Length > 1 ? parts[1] : string.Empty));
        }
    }

    public SavedTab? LoadTab(Guid id) => this.library?.Get(id);

    public void DeleteTab(Guid id)
    {
        this.library?.Delete(id);
        // LoadAllAsync fires via Changed observable
    }

    public string SearchQuery
    {
        get => this.searchQuery;
        set
        {
            if (this.SetProperty(ref this.searchQuery, value))
            {
                _ = this.FilterResultsAsync();
                this.OnPropertyChanged(nameof(TabListViewModel.IsSearching));
                this.OnPropertyChanged(nameof(TabListViewModel.HasNoResults));
            }
        }
    }

    public bool IsSearching    => !string.IsNullOrWhiteSpace(this.searchQuery);
    public bool HasNoResults   => this.IsSearching && !this.HasSearchResults;

    public TabReferenceViewModel? SelectedTabReference
    {
        get => this.selectedTabReference;
        set => this.SetProperty(ref this.selectedTabReference, value);
    }

    public ObservableCollection<TabReferenceViewModel> AllTabs { get; } = new();
    public ObservableCollection<TabReferenceViewModel> SearchResults { get; } = new();
    public bool HasSearchResults => this.SearchResults.Count > 0;

    private async Task FilterResultsAsync()
    {
        this.filterCts.Cancel();
        this.filterCts = new CancellationTokenSource();
        var ct = this.filterCts.Token;

        this.SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(this.searchQuery) || this.library == null)
        {
            return;
        }

        try
        {
            var results = await Task.Run(() =>
                this.library.Search(this.searchQuery).OrderBy(e => SongPart(e.Name)).ToList(), ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            foreach (var (id, name) in results)
            {
                var parts = name.Split(" - ", 2);
                this.SearchResults.Add(new TabReferenceViewModel(id,
                    parts.Length > 1 ? parts[0] : name,
                    parts.Length > 1 ? parts[1] : string.Empty));
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── Import ───────────────────────────────────────────────────────────────

    public bool IsImporting
    {
        get => this.isImporting;
        private set {
            this.SetProperty(ref this.isImporting, value);
            this.OnPropertyChanged(nameof(TabListViewModel.CanImport)); }
    }
    public bool CanImport => !this.isImporting;
    public int ImportCurrent { get => this.importCurrent; private set {
        this.SetProperty(ref this.importCurrent, value);
        this.OnPropertyChanged(nameof(TabListViewModel.ImportProgressText)); } }
    public int ImportTotal   { get => this.importTotal;   private set {
        this.SetProperty(ref this.importTotal,   value);
        this.OnPropertyChanged(nameof(TabListViewModel.ImportProgressText)); } }
    public string ImportProgressText => $"{this.importCurrent} / {this.importTotal}";
    public string ImportStatusMessage { get; private set; } = string.Empty;
    public bool HasImportStatus => !string.IsNullOrEmpty(this.ImportStatusMessage);

    public void BeginImport(int total)
    {
        this.ImportTotal = total;
        this.ImportCurrent = 0;
        this.ImportStatusMessage = string.Empty;
        this.OnPropertyChanged(nameof(TabListViewModel.ImportStatusMessage));
        this.OnPropertyChanged(nameof(TabListViewModel.HasImportStatus));
        this.IsImporting = true;
    }

    public async Task<bool> ImportOneAsync(string fileName, string content)
    {
        if (this.library == null)
        {
            return false;
        }
        var saved = await Task.Run(() =>
        {
            var tab = WikiTabParser.Parse(fileName, content);
            if (tab == null)
            {
                return false;
            }
            var version = WikiTabParser.ParseVersion(fileName);
            var name    = WikiTabParser.StoreKey(tab.Artist, tab.Song, version);
            if (this.library.NameExists(name))
            {
                return false;
            }
            this.library.Save(Guid.NewGuid(), name, tab);
            return true;
        });
        this.ImportCurrent++;
        return saved;
    }

    public void EndImport(int saved)
    {
        this.IsImporting = false;
        var skipped = this.importCurrent - saved;
        this.ImportStatusMessage = saved > 0
            ? skipped > 0 ? $"Imported {saved}, skipped {skipped} duplicate{(skipped == 1 ? "" : "s")}"
                          : $"Imported {saved} tab{(saved == 1 ? "" : "s")}"
            :
            this.importCurrent > 0 ? "All files already imported"
                                : "No valid tabs found";
        this.OnPropertyChanged(nameof(TabListViewModel.ImportStatusMessage));
        this.OnPropertyChanged(nameof(TabListViewModel.HasImportStatus));
    }

    private static string SongPart(string name)
    {
        var idx = name.IndexOf(" - ", StringComparison.Ordinal);
        return idx >= 0 ? name[(idx + 3)..] : name;
    }

    public void Dispose()
    {
        this.filterCts.Dispose();
        this.disposables.Dispose();
    }
}
