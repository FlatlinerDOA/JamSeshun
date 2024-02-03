using Avalonia.Controls;
using Avalonia.Interactivity;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;
    }
}
