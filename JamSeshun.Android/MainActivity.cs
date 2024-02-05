using Android.App;
using Android.Content.PM;

using Avalonia;
using Avalonia.Android;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Android;

[Activity(
    Label = "JamSeshun.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITuningService, AndroidTuningService>();
            })
            .WithInterFont();
    }
}
