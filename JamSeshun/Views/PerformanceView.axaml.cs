using Avalonia.Controls;

namespace JamSeshun.Views;

using ViewModels;

public partial class PerformanceView : UserControl
{
    private readonly IDisposable wiring;
    public PerformanceView()
    {
        this.InitializeComponent();
        this.wiring = this.WireOnShow();
    }
}
