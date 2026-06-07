using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabListView : UserControl
{
    private readonly IDisposable wiring;

    public TabListView()
    {
        this.InitializeComponent();
        this.wiring = this.WireOnShow();
    }
}
