using Avalonia.Controls;
using JamSeshun.ViewModels;
using JamSeshun.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Services;

public static class ServiceRegistry
{
    public static IServiceProvider ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();

        // Register your application services with the container
        serviceCollection.AddSingleton<GuitarTabsService>();

        // Add view models and views as necessary.
        serviceCollection.AddTransient<MainViewModel>();

        // Register the template for MainViewModel which will be resolved by the ViewLocator via a naming convention replacing "ViewModel" with "View".
        serviceCollection.AddKeyedTransient<Control, MainView>("MainView");

        // Here we can register the MainWindow resolved by the app, this allows your MainWindow have services injected too.
        serviceCollection.AddKeyedTransient<Window, MainWindow>("MainWindow");

        return serviceCollection.BuildServiceProvider();
    }
}
