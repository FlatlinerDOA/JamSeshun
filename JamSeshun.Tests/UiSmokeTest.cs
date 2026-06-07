using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using JamSeshun.Services;
using JamSeshun.ViewModels;
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
        var window = new Window { Width = 400, Height = 700 };
        var vm = new TunerViewModel();
        vm.CurrentNote = "A";
        vm.CurrentFrequency = 438.2f;
        vm.CurrentErrorInCents = -8.5f;
        vm.CurrentErrorInDegrees = -9.4f;
        vm.Confidence = 0.9f;
        window.Content = new TunerView { DataContext = vm };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/tuner-view.png");
        frame.Save(fs);
    }

    [AvaloniaFact]
    public void TabListView_ShouldRender()
    {
        var window = new Window { Width = 400, Height = 700 };
        window.Content = new TabListView { DataContext = new TabListViewModel() };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/tab-list-view.png");
        frame.Save(fs);
    }

    [AvaloniaFact]
    public void TabView_ShouldRender()
    {
        var sampleTab = new SavedTab(
            Artist: "Oasis",
            Song: "Wonderwall",
            Content: SampleTabContent,
            Tuning: "Standard (EADGBe)",
            Capo: 0,
            DateSaved: DateTimeOffset.Now);

        var vm = new TabViewModel { Tab = sampleTab };
        var window = new Window { Width = 400, Height = 700 };
        window.Content = new TabView { DataContext = vm };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/tab-view.png");
        frame.Save(fs);
    }

    [AvaloniaFact]
    public void TabEditorView_ShouldRender()
    {
        var window = new Window { Width = 400, Height = 700 };
        window.Content = new TabEditorView { DataContext = new TabEditorViewModel() };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory("/tmp/jamseshun-ui");
        using var fs = File.OpenWrite("/tmp/jamseshun-ui/tab-editor-view.png");
        frame.Save(fs);
    }

    private const string SampleTabContent = @"Intro: Em7 G Dsus4 A7sus4

Verse 1:
Em7              G
Today is gonna be the day
              Dsus4           A7sus4
That they're gonna throw it back to you

Chorus:
  Cadd9          G
Because maybe
             Dsus4                A7sus4
You're gonna be the one that saves me

Tab (Intro):
e|--3--3--3--3--3--3--3--3--|
B|--3--3--3--3--3--3--3--3--|
G|--0--0--2--2--2--2--0--0--|
D|--0--2--0--0--0--0--2--0--|
A|--2--2--0--0--0--0--2--0--|
E|--0--0--x--x--0--0--x--x--|";

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
