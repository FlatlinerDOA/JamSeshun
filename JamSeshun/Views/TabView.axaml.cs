using Avalonia.Controls;
using Avalonia.VisualTree;
using JamSeshun.Services.Tuning;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class TabView : UserControl
{
    public TabView()
    {
        InitializeComponent();

        BackButton.Click += async (_, _) =>
        {
            var nav = this.FindAncestorOfType<NavigationPage>();
            if (nav != null)
                await nav.PopAsync();
        };

        EditButton.Click += async (_, _) =>
        {
            var tabVm = DataContext as TabViewModel;
            if (tabVm?.Tab == null || tabVm.Id == null) return;

            var nav = this.FindAncestorOfType<NavigationPage>();
            if (nav == null) return;

            var editorVm = App.ServiceProvider.GetRequiredService<TabEditorViewModel>();
            editorVm.LoadForEdit(tabVm.Id.Value, tabVm.Tab);

            var editorView  = new TabEditorView { DataContext = editorVm };
            var contentPage = new ContentPage { Content = editorView };
            NavigationPage.SetHasNavigationBar(contentPage, false);

            await nav.PushAsync(contentPage);
        };

        TuneButton.Click += (_, _) =>
        {
            var tabVm = DataContext as TabViewModel;
            var tuning = GuitarTuning.TryParse(tabVm?.Tab?.Tuning);
            if (tuning == null) return;

            var mainView = this.FindAncestorOfType<MainView>();
            if (mainView?.DataContext is MainViewModel mainVm)
                mainVm.TunerVM.TargetTuning = tuning;

            var tabbedPage = this.FindAncestorOfType<TabbedPage>();
            if (tabbedPage != null)
                tabbedPage.SelectedIndex = 0;
        };
    }
}
