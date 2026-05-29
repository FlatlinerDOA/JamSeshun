using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
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
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Xamarin.Essentials.Platform.Init(this, savedInstanceState);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }
}

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer) { }

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
