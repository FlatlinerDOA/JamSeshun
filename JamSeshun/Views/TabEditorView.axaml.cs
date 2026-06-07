using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Reactive.Disposables;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabEditorView : UserControl
{
    private readonly IDisposable wiring;

    public TabEditorView()
    {
        this.InitializeComponent();

        this.wiring = this.WireOnShow(dataContext =>
        {
            if (dataContext is not TabEditorViewModel vm)
            {
                return null;
            }
            return new CompositeDisposable
            {
                vm.Saved.Subscribe(async void (_) => await this.PopAsync()),
                vm.Deleted.Subscribe(async void (_) =>
                {
                    var nav = this.FindAncestorOfType<NavigationPage>();
                    if (nav == null)
                    {
                        return;
                    }
                    await nav.PopAsync(); // editor → tab viewer
                    await nav.PopAsync(); // tab viewer → songs list
                })
            };
        });

        Observable.FromEventPattern<RoutedEventArgs>(
                h => this.BackButton.Click += h, h => this.BackButton.Click -= h)
            .Subscribe(async void (_) => await this.PopAsync());
    }

    private async Task PopAsync()
    {
        var nav = this.FindAncestorOfType<NavigationPage>();
        if (nav != null)
        {
            await nav.PopAsync();
        }
    }
}
