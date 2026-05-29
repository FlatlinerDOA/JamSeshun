using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using JamSeshun.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Tests;

public class UiSmokeTest
{
    [AvaloniaFact]
    public void MainWindow_ShouldRender()
    {
        var window = App.ServiceProvider.GetRequiredKeyedService<Window>("MainWindow");
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/main-window.png");
        frame.Save(fs);
    }

    [AvaloniaFact]
    public void TunerView_ShouldRender()
    {
        var window = new Window { Width = 800, Height = 600 };
        window.Content = new TunerView();
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/tuner-view.png");
        frame.Save(fs);
    }

    [AvaloniaFact]
    public void TunerView_ClickStart_ShouldNotCrash()
    {
        var window = new Window { Width = 800, Height = 600 };
        window.Content = new TunerView();
        window.Show();

        var startButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Content?.ToString()?.Contains("Start", StringComparison.OrdinalIgnoreCase) == true);

        if (startButton is not null)
        {
            var bounds = startButton.Bounds;
            var center = bounds.Center;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
        }

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/tuner-after-start.png");
        frame.Save(fs);
    }
}
