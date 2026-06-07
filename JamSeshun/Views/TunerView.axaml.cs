using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JamSeshun.Services;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TunerView : UserControl
{
    private readonly IDisposable wiring;

    public TunerView()
    {
        this.InitializeComponent();
        this.wiring = this.WireOnShow();
        this.StringGuide.AsObservable(PointerPressedEvent, RoutingStrategies.Bubble)
            .Subscribe(this.OnStringGuideTapped);
    }

    public TunerView(TunerViewModel viewModel) : this()
    {
        this.DataContext = viewModel;
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
