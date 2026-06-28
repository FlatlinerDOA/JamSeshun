using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Foundation;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using Microsoft.Extensions.DependencyInjection;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

namespace JamSeshun.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        return base.CustomizeAppBuilder(builder)
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITuningService, IosTuningService>();
            })
            .WithInterFont()
            .AfterSetup(_ =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
                {
                    singleView.MainView = App.ServiceProvider.GetRequiredKeyedService<Control>("MainView");
                }
            });
    }
}
