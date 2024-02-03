using Avalonia.Controls.Platform;
using JamSeshun.Services;
using System.Collections.ObjectModel;

namespace JamSeshun.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly GuitarTabsService tabsService;

    public MainViewModel()
    {
        this.Favorites.Add(new(new TabReference("Frank Sinatra", "Come Fly With Me", 1, "Tab", 100, 0.5m, null)));
        this.Favorites.Add(new(new TabReference("The Beatles", "Come Together", 1, "Tab", 1400, 0.7m, null)));
    }

    internal MainViewModel(GuitarTabsService tabsService)
    {
        this.tabsService = tabsService;
    }

    public string Greeting => "Welcome to Jam Seshun!";

    public ObservableCollection<TabReferenceViewModel> Favorites { get; private set; } = new ObservableCollection<TabReferenceViewModel>();
}

