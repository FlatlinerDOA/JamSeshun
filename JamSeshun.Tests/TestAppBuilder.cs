using Avalonia;
using Avalonia.Headless;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using JamSeshun.Tests;
using Microsoft.Extensions.DependencyInjection;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace JamSeshun.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont()
        .ConfigureServices(services =>
        {
            services.AddSingleton<ITuningService, NullTuningService>();
        });
}
