using Avalonia;
using Avalonia.Controls;
using JamSeshun.ViewModels;
using JamSeshun.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Services;

public static class ServiceRegistry
{
    public static ServiceCollection WithViewsAndViewModels(this ServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<TunerViewModel>();
        serviceCollection.AddTransient<TabListViewModel>();
        serviceCollection.AddTransient<PerformanceViewModel>();
        serviceCollection.AddTransient<TabEditorViewModel>();
        serviceCollection.AddTransient<MainViewModel>();

        serviceCollection.AddKeyedTransient<Control, TunerView>("TunerView");
        serviceCollection.AddKeyedTransient<Control, TabListView>("TabListView");
        serviceCollection.AddKeyedTransient<Control, PerformanceView>("PerformanceView");
        serviceCollection.AddKeyedTransient<Control, TabEditorView>("TabEditorView");
        serviceCollection.AddKeyedTransient<Control, MainView>("MainView");

        serviceCollection.AddKeyedTransient<Window, MainWindow>("MainWindow");
        return serviceCollection;
    }

    public static ServiceCollection WithCommonServices(this ServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<TabLibraryService>();
        return serviceCollection;
    }

    public static AppBuilder ConfigureServices(this AppBuilder appBuilder, Action<ServiceCollection> configure)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.WithCommonServices().WithViewsAndViewModels();
        configure(serviceCollection);
        App.ServiceProvider = serviceCollection.BuildServiceProvider();
        return appBuilder.With<IServiceProvider>(() => App.ServiceProvider);
    }
}
