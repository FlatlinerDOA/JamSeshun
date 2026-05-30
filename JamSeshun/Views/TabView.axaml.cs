using Avalonia.Controls;
using Avalonia.VisualTree;

namespace JamSeshun.Views;

public partial class TabView : UserControl
{
    public TabView()
    {
        InitializeComponent();
        BackButton.Click += async (_, _) =>
        {
            var nav = this.FindAncestorOfType<NavigationPage>();
            if (nav != null)
                await nav.PopAsync();
        };
    }
}
