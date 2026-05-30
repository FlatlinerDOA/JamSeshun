using Avalonia.Controls;
using Avalonia.VisualTree;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabListView : UserControl
{
    public TabListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private TabListViewModel? _vm;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as TabListViewModel;

        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private async void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TabListViewModel.SelectedTabReference)) return;

        var tabRef = _vm?.SelectedTabReference;
        if (tabRef == null) return;

        var navPage = this.FindAncestorOfType<NavigationPage>();
        if (navPage == null) return;

        var vm = new TabViewModel();
        var tabView = new TabView { DataContext = vm };
        var contentPage = new ContentPage
        {
            Header = $"{tabRef.Artist} — {tabRef.Song}",
            Content = tabView
        };

        await navPage.PushAsync(contentPage);

        var savedTab = _vm?.LoadTab(tabRef.Id);
        if (savedTab != null)
            vm.Tab = savedTab;
    }
}
