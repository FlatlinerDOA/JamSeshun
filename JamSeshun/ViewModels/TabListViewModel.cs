using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public partial class TabListViewModel : ViewModelBase
{
    private readonly TabLibraryService? _library;
    private string searchQuery = string.Empty;
    private TabReferenceViewModel? selectedTabReference;

    public TabListViewModel()
    {
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
        set { SetProperty(ref searchQuery, value); FilterResults(); }
    }

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

    private static string SongPart(string name)
    {
        var idx = name.IndexOf(" - ", StringComparison.Ordinal);
        return idx >= 0 ? name[(idx + 3)..] : name;
    }
}
