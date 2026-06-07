using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Reactive.Disposables;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabEditorView : UserControl
{
    private readonly CompositeDisposable disposables = new();
    private IDisposable? vmSubscription;

    public TabEditorView()
    {
        this.InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        this.disposables.Add(
            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.BackButton.Click += h, h => this.BackButton.Click -= h)
                .Subscribe(async void (_) => await this.PopAsync())
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
        if (this.DataContext is not TabEditorViewModel vm)
        {
            return;
        }

        var vmDisposables = new CompositeDisposable
        {
            Observable.FromEvent(h => vm.Saved += h, h => vm.Saved -= h)
                .Subscribe(async void (_) => await this.PopAsync()),

            Observable.FromEvent(h => vm.Deleted += h, h => vm.Deleted -= h)
                .Subscribe(async void (_) =>
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
        this.vmSubscription = vmDisposables;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        this.vmSubscription?.Dispose();
        this.vmSubscription = null;
        this.disposables.Clear();
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
