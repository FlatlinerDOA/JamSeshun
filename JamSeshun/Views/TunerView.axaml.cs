using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Reactive.Disposables;
using Avalonia.VisualTree;
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
        this.StringGuide.AddHandler(PointerPressedEvent, this.OnStringGuideTapped, RoutingStrategies.Bubble);
        this.disposables.Add(Disposable.Create(() => this.StringGuide.RemoveHandler(PointerPressedEvent, this.OnStringGuideTapped)));

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

    private void OnStringGuideTapped(object? sender, PointerPressedEventArgs e)
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
