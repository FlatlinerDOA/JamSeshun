using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using JamSeshun.Services;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class TabListView : UserControl
{
    public TabListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        NewTabButton.Click += OnNewTabClicked;
        ImportButton.Click += OnImportClicked;
    }

    private TabListViewModel? _vm;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as TabListViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private async void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TabListViewModel.SelectedTabReference))
        {
            return;
        }

        var tabRef = _vm?.SelectedTabReference;
        if (tabRef == null)
        {
            return;
        }

        var navPage = this.FindAncestorOfType<NavigationPage>();
        if (navPage == null)
        {
            return;
        }

        var library = App.ServiceProvider.GetRequiredService<TabLibraryService>();
        var vm = new TabViewModel(library);
        var tabView = new TabView { DataContext = vm };
        var contentPage = new ContentPage { Content = tabView };
        NavigationPage.SetHasNavigationBar(contentPage, false);

        await navPage.PushAsync(contentPage);

        if (_vm != null)
        {
            _vm.SelectedTabReference = null;
        }

        var savedTab = _vm?.LoadTab(tabRef.Id);
        if (savedTab != null)
        {
            vm.Id  = tabRef.Id;
            vm.Tab = savedTab;
        }
    }

    private async void OnNewTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var navPage = this.FindAncestorOfType<NavigationPage>();
        if (navPage == null)
        {
            return;
        }

        var editorVm = App.ServiceProvider.GetRequiredService<TabEditorViewModel>();
        var editorView = new TabEditorView { DataContext = editorVm };
        var contentPage = new ContentPage { Content = editorView };
        NavigationPage.SetHasNavigationBar(contentPage, false);

        await navPage.PushAsync(contentPage);
    }

    private static readonly FilePickerFileType TxtFiles = new("Tab files")
    {
        Patterns = ["*.txt"],
        MimeTypes = ["text/plain"]
    };

    private async void OnImportClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Tabs",
            AllowMultiple = true,
            FileTypeFilter = [TxtFiles]
        });

        if (files.Count == 0)
        {
            return;
        }

        _vm.BeginImport(files.Count);
        var saved = 0;
        try
        {
            foreach (var file in files)
            {
                string content;
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
                if (await _vm.ImportOneAsync(file.Name, content))
                {
                    saved++;
                }
            }
        }
        finally
        {
            _vm.EndImport(saved);
        }
    }
}
