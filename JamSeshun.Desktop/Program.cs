using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITuningService>(new WindowsTuningService());
            })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(a => 
            {                
                if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = App.ServiceProvider.GetKeyedService<Window>("MainWindow");
                }
                else if (App.Current.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
                {
                    singleViewPlatform.MainView = App.ServiceProvider.GetKeyedService<Control>("MainView");
                }
            });

}
