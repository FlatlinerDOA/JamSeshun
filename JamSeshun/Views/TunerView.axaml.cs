using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TunerView : UserControl
{
    public TunerView()
    {
        InitializeComponent();
        StringGuide.AddHandler(PointerPressedEvent, OnStringGuideTapped, RoutingStrategies.Bubble);
    }

    public TunerView(TunerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnStringGuideTapped(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TunerViewModel vm)
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
