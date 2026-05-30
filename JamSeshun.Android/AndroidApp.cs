namespace JamSeshun.Android;

using System;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using global::Android.App;
using global::Android.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Services;
using Services.Tuning;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        return base.CustomizeAppBuilder(builder)
            .ConfigureServices(services =>
            {
                ServiceCollectionServiceExtensions.AddSingleton<ITuningService, AndroidTuningService>(services);
            })
            .WithInterFont()
            .AfterSetup(_ =>
            {
                if (App.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
                    singleView.MainView = App.ServiceProvider.GetKeyedService<Control>("MainView");
            });
    }
}
