using Avalonia;
using Avalonia.Controls;
using JamSeshun.Services;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        this.InitializeComponent();
    }

    public MainView(MainViewModel viewModel)
    {
        this.InitializeComponent();
        this.DataContext = viewModel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        App.ServiceProvider?.GetRequiredService<NavigationService>()
            .Register(this.songsNav, this.mainTabs);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            App.ServiceProvider?.GetRequiredService<FilePickerService>()
                .Register(topLevel);
        }
    }
}
