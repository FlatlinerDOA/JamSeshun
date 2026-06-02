using Avalonia.Controls;
using Avalonia.VisualTree;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabEditorView : UserControl
{
    public TabEditorView()
    {
        InitializeComponent();

        BackButton.Click += async (_, _) => await PopAsync();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is TabEditorViewModel vm)
            {
                vm.Saved   += async () => await PopAsync();
                vm.Deleted += async () =>
                {
                    var nav = this.FindAncestorOfType<NavigationPage>();
                    if (nav == null)
                    {
                        return;
                    }
                    await nav.PopAsync(); // editor → tab viewer
                    await nav.PopAsync(); // tab viewer → songs list
                };
            }
        };
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
