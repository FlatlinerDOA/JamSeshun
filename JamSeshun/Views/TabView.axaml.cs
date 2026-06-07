using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Reactive.Disposables;
using JamSeshun.Services.Tuning;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class TabView : UserControl
{
    private readonly CompositeDisposable disposables = new();
    private IDisposable? editorSavedSubscription;

    public TabView()
    {
        this.InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        this.disposables.Add(
            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.BackButton.Click += h, h => this.BackButton.Click -= h)
                .Subscribe(async void (_) =>
                {
                    var nav = this.FindAncestorOfType<NavigationPage>();
                    if (nav != null)
                    {
                        await nav.PopAsync();
                    }
                })
        );

        this.disposables.Add(
            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.EditButton.Click += h, h => this.EditButton.Click -= h)
                .Subscribe(async void (_) => await this.OnEditClicked())
        );

        this.disposables.Add(
            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.TuneButton.Click += h, h => this.TuneButton.Click -= h)
                .Subscribe(_ => this.OnTuneClicked())
        );
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        this.editorSavedSubscription?.Dispose();
        this.editorSavedSubscription = null;
        this.disposables.Clear();
    }

    private async Task OnEditClicked()
    {
        var tabVm = this.DataContext as TabViewModel;
        if (tabVm?.Tab == null || tabVm.Id == null)
        {
            return;
        }

        var nav = this.FindAncestorOfType<NavigationPage>();
        if (nav == null)
        {
            return;
        }

        var editorVm = App.ServiceProvider.GetRequiredService<TabEditorViewModel>();
        editorVm.LoadForEdit(tabVm.Id.Value, tabVm.Tab);

        this.editorSavedSubscription?.Dispose();
        this.editorSavedSubscription = Observable.FromEvent(
                h => editorVm.Saved += h, h => editorVm.Saved -= h)
            .Subscribe(_ => tabVm.ReloadTab());

        var editorView  = new TabEditorView { DataContext = editorVm };
        var contentPage = new ContentPage { Content = editorView };
        NavigationPage.SetHasNavigationBar(contentPage, false);

        await nav.PushAsync(contentPage);
    }

    private void OnTuneClicked()
    {
        var tabVm = this.DataContext as TabViewModel;
        var tuning = GuitarTuning.TryParse(tabVm?.Tab?.Tuning);
        if (tuning == null)
        {
            return;
        }

        var mainView = this.FindAncestorOfType<MainView>();
        if (mainView?.DataContext is MainViewModel mainVm)
        {
            mainVm.TunerVm.TargetTuning = tuning;
        }

        var tabbedPage = this.FindAncestorOfType<TabbedPage>();
        if (tabbedPage != null)
        {
            tabbedPage.SelectedIndex = 0;
        }
    }
}
