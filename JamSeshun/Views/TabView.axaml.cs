using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabView : UserControl
{
    private readonly IDisposable wiring;

    public TabView()
    {
        this.InitializeComponent();
        this.wiring = this.WireOnShow();
    }
}
