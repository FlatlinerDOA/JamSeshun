using Avalonia;
using Avalonia.Controls;
using JamSeshun.Services.Tuning;
using JamSeshun.ViewModels;
using JamSeshun.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Services;

public static class ServiceRegistry
{
    public static ServiceCollection WithViewsAndViewModels(this ServiceCollection serviceCollection)
    {
        // Add view models and views as necessary.
        serviceCollection.AddTransient<MainViewModel>();
        serviceCollection.AddTransient<TunerViewModel>();
        serviceCollection.AddTransient<PerformanceViewModel>();
        serviceCollection.AddTransient<TabListViewModel>();


        serviceCollection.AddKeyedTransient<Control, TunerView>("PerformanceView");
        serviceCollection.AddKeyedTransient<Control, TunerView>("TabListView");
        serviceCollection.AddKeyedTransient<Control, TunerView>("TunerView");

        // Register the template for MainViewModel which will be resolved by the ViewLocator via a naming convention replacing "ViewModel" with "View".
        serviceCollection.AddKeyedTransient<Control, MainView>("MainView");

        // Here we can register the MainWindow resolved by the app, this allows your MainWindow have services injected too.
        serviceCollection.AddKeyedTransient<Window, MainWindow>("MainWindow");
        return serviceCollection;
    }

    public static ServiceCollection WithCommonServices(this ServiceCollection serviceCollection)
    {
        // Register your application services with the container
        serviceCollection.AddSingleton<GuitarTabsService>();
        return serviceCollection;
    }

    public static AppBuilder ConfigureServices(this AppBuilder appBuilder, Action<ServiceCollection> configure)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.WithCommonServices().WithViewsAndViewModels();
        configure(serviceCollection);
        App.ServiceProvider = serviceCollection.BuildServiceProvider();
        return appBuilder.With<IServiceProvider>(() =>
        {
            return App.ServiceProvider;
        });
    }
}
