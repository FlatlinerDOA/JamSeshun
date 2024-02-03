using Avalonia.Controls;
using Avalonia.Interactivity;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel context)
    {
        InitializeComponent();
        this.DataContext = context;
    }
}
