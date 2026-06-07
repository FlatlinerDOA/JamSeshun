using System.ComponentModel;
using System.Reactive.Disposables;
using Avalonia;
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
    private readonly CompositeDisposable disposables = new();
    private IDisposable? vmSubscription;
    private TabListViewModel? vm;

    public TabListView()
    {
        this.InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        this.disposables.Add(
            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.NewTabButton.Click += h, h => this.NewTabButton.Click -= h)
                .Subscribe(async void (_) => await this.OnNewTabClicked())
        );

        this.disposables.Add(
            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.ImportButton.Click += h, h => this.ImportButton.Click -= h)
                .Subscribe(async void (_) => await this.OnImportClicked())
        );

        this.disposables.Add(
            Observable.FromEventPattern(this, nameof(DataContextChanged))
                .Subscribe(_ => this.OnDataContextUpdated())
        );

        this.OnDataContextUpdated();
    }

    private void OnDataContextUpdated()
    {
        this.vmSubscription?.Dispose();
        this.vm = this.DataContext as TabListViewModel;
        if (this.vm == null)
        {
            return;
        }

        _ = this.vm.LoadAllAsync();

        this.vmSubscription = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => this.vm.PropertyChanged += h,
                h => this.vm.PropertyChanged -= h)
            .Where(ep => ep.EventArgs.PropertyName == nameof(TabListViewModel.SelectedTabReference))
            .Subscribe(async void (_) => await this.OnSelectedTabReferenceChanged());
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        this.vmSubscription?.Dispose();
        this.vmSubscription = null;
        this.disposables.Clear();
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
        var vm = new TabViewModel(library);
        var tabView = new TabView { DataContext = vm };
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
            vm.Id  = tabRef.Id;
            vm.Tab = savedTab;
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

    private static readonly FilePickerFileType TxtFiles = new("Tab files")
    {
        Patterns = ["*.txt"],
        MimeTypes = ["text/plain"]
    };

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
            FileTypeFilter = [TxtFiles]
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
