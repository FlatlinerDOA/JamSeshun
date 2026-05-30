using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public partial class TabListViewModel : ViewModelBase
{
    private readonly TabLibraryService? _library;
    private string searchQuery = string.Empty;
    private bool isSearching;
    private TabReferenceViewModel? selectedTabReference;

    public TabListViewModel()
    {
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SearchResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSearchResults));
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
        foreach (var (id, name) in _library.GetAll())
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
            SetProperty(ref searchQuery, value);
            if (string.IsNullOrWhiteSpace(value))
                SearchResults.Clear();
        }
    }

    public bool IsSearching
    {
        get => isSearching;
        private set => SetProperty(ref isSearching, value);
    }

    public TabReferenceViewModel? SelectedTabReference
    {
        get => selectedTabReference;
        set => SetProperty(ref selectedTabReference, value);
    }

    public ObservableCollection<TabReferenceViewModel> AllTabs { get; } = new();
    public ObservableCollection<TabReferenceViewModel> SearchResults { get; } = new();
    public bool HasSearchResults => SearchResults.Count > 0;

    public IAsyncRelayCommand SearchCommand { get; }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || _library == null)
            return;

        IsSearching = true;
        SearchResults.Clear();
        try
        {
            await Task.Yield();
            foreach (var (id, name) in _library.Search(SearchQuery))
            {
                var parts = name.Split(" - ", 2);
                SearchResults.Add(new TabReferenceViewModel(id,
                    parts.Length > 1 ? parts[0] : name,
                    parts.Length > 1 ? parts[1] : string.Empty));
            }
        }
        finally
        {
            IsSearching = false;
        }
    }
}
