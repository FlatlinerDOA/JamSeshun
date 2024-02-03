using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using JamSeshun.ViewModels;
using JamSeshun.Views;
using JamSeshun.Services;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
namespace JamSeshun;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } 

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceProvider = ServiceRegistry.ConfigureServices();

        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = ServiceProvider.GetKeyedService<Window>("MainWindow");
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = ServiceProvider.GetKeyedService<Control>("MainView");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
