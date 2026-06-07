using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabEditorView : UserControl
{
    private readonly IDisposable wiring;

    public TabEditorView()
    {
        this.InitializeComponent();
        this.wiring = this.WireOnShow();
    }
}
