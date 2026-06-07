using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Reactive.Disposables;
using JamSeshun.Services;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TunerView : UserControl
{
    private readonly CompositeDisposable disposables = new();

    public TunerView()
    {
        this.InitializeComponent();
    }

    public TunerView(TunerViewModel viewModel) : this()
    {
        this.DataContext = viewModel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        this.disposables.Add(
            this.StringGuide.AsObservable(PointerPressedEvent, RoutingStrategies.Bubble)
                .Subscribe(this.OnStringGuideTapped)
        );

        if (this.DataContext is TunerViewModel vm)
        {
            vm.Load();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        this.disposables.Clear();
    }

    private void OnStringGuideTapped(PointerPressedEventArgs e)
    {
        if (this.DataContext is not TunerViewModel vm)
        {
            return;
        }

        var source = e.Source as Visual;
        while (source != null)
        {
            if (source is StyledElement { DataContext: TuningStringDisplay display })
            {
                vm.ToggleLockString(display.Index);
                e.Handled = true;
                return;
            }
            source = source.GetVisualParent();
        }
    }
}
