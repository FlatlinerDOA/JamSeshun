using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public partial class TabListViewModel : ViewModelBase
{
    private readonly GuitarTabsService? tabsService;
    private string searchQuery = string.Empty;
    private bool isSearching;
    private TabReferenceViewModel? selectedTabReference;

    public TabListViewModel()
    {
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SearchResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSearchResults));
        Favorites.Add(new(new TabReference("Frank Sinatra", "Come Fly With Me", 1, "Tab", 100, 0.5m, (string?)null)));
        Favorites.Add(new(new TabReference("The Beatles", "Come Together", 1, "Tab", 1400, 0.7m, (string?)null)));
    }

    public TabListViewModel(GuitarTabsService tabsService) : this()
    {
        this.tabsService = tabsService;
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

    public ObservableCollection<TabReferenceViewModel> Favorites { get; } = new();
    public ObservableCollection<TabReferenceViewModel> SearchResults { get; } = new();

    public bool HasSearchResults => SearchResults.Count > 0;

    public IAsyncRelayCommand SearchCommand { get; }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || tabsService == null)
            return;

        IsSearching = true;
        SearchResults.Clear();
        try
        {
            var results = await tabsService.SearchAsync(SearchQuery);
            foreach (var r in results)
                SearchResults.Add(new TabReferenceViewModel(r));
        }
        finally
        {
            IsSearching = false;
        }
    }
}
