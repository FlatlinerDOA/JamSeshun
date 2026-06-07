using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JamSeshun.Services.Tuning;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class TabView : UserControl
{
    private readonly IDisposable wiring;

    public TabView()
    {
        this.InitializeComponent();
        this.wiring = this.WireOnShow();

        Observable.FromEventPattern<RoutedEventArgs>(
                h => this.BackButton.Click += h, h => this.BackButton.Click -= h)
            .Subscribe(async void (_) =>
            {
                var nav = this.FindAncestorOfType<NavigationPage>();
                if (nav != null)
                {
                    await nav.PopAsync();
                }
            });

        Observable.FromEventPattern<RoutedEventArgs>(
                h => this.EditButton.Click += h, h => this.EditButton.Click -= h)
            .Subscribe(async void (_) => await this.OnEditClicked());

        Observable.FromEventPattern<RoutedEventArgs>(
                h => this.TuneButton.Click += h, h => this.TuneButton.Click -= h)
            .Subscribe(_ => this.OnTuneClicked());
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
