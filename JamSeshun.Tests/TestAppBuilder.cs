using Avalonia;
using Avalonia.Headless;
using JamSeshun.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace JamSeshun.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions() { UseHeadlessDrawing = false });
}
