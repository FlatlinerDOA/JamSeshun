using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using JamSeshun.Services;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class TabListView : UserControl
{
    private static readonly FilePickerFileType tabFileTypes = new("Tab files")
    {
        Patterns = ["*.txt"],
        MimeTypes = ["text/plain"]
    };

    private readonly IDisposable wiring;
    private TabListViewModel? vm;

    public TabListView()
    {
        this.InitializeComponent();

        this.wiring = this.WireOnShow(dataContext =>
        {
            this.vm = dataContext as TabListViewModel;
            if (this.vm == null)
            {
                return null;
            }
            return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    h => this.vm.PropertyChanged += h,
                    h => this.vm.PropertyChanged -= h)
                .Where(ep => ep.EventArgs.PropertyName == nameof(TabListViewModel.SelectedTabReference))
                .Subscribe(async void (_) => await this.OnSelectedTabReferenceChanged());
        });

        Observable.FromEventPattern<RoutedEventArgs>(
                h => this.NewTabButton.Click += h, h => this.NewTabButton.Click -= h)
            .Subscribe(async void (_) => await this.OnNewTabClicked());

        Observable.FromEventPattern<RoutedEventArgs>(
                h => this.ImportButton.Click += h, h => this.ImportButton.Click -= h)
            .Subscribe(async void (_) => await this.OnImportClicked());
    }

    private async Task OnSelectedTabReferenceChanged()
    {
        var tabRef = this.vm?.SelectedTabReference;
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
        var tabVm = new TabViewModel(library);
        var tabView = new TabView { DataContext = tabVm };
        var contentPage = new ContentPage { Content = tabView };
        NavigationPage.SetHasNavigationBar(contentPage, false);

        await navPage.PushAsync(contentPage);

        if (this.vm != null)
        {
            this.vm.SelectedTabReference = null;
        }

        var savedTab = this.vm?.LoadTab(tabRef.Id);
        if (savedTab != null)
        {
            tabVm.Id  = tabRef.Id;
            tabVm.Tab = savedTab;
        }
    }

    private async Task OnNewTabClicked()
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

    private async Task OnImportClicked()
    {
        if (this.vm == null)
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
            FileTypeFilter = [tabFileTypes]
        });

        if (files.Count == 0)
        {
            return;
        }

        this.vm.BeginImport(files.Count);
        var saved = 0;
        try
        {
            foreach (var file in files)
            {
                string content;
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync();
                if (await this.vm.ImportOneAsync(file.Name, content))
                {
                    saved++;
                }
            }
        }
        finally
        {
            this.vm.EndImport(saved);
        }
    }
}
