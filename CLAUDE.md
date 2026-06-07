# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Target State

Running **.NET 10** and **Avalonia 12.0.4**. The app is a personal guitar utility with two target features for v1:
- **Guitar Tuner** — real-time pitch detection via microphone
- **Guitar Chords / Tab viewer** — browsing and displaying tabs

## Build & Run

```bash
# Build everything
dotnet build

# Run desktop app (Windows/Linux)
dotnet run --project JamSeshun.Desktop

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~FftPitchDetectorSpecification"

# Android build (requires Android SDK)
dotnet build JamSeshun.Android
```

## Project Structure

| Project | Purpose                                                                                          |
|---|--------------------------------------------------------------------------------------------------|
| `JamSeshun/` | Shared UI library — views, view models, services (cross-platform)                                |
| `JamSeshun.Desktop/` | Desktop entry point; registers `SoundFlowTuningService` (SoundFlow Cross Platform Audio library) |
| `JamSeshun.Android/` | Android entry point; registers `AndroidTuningService` (Native Android Audio)                     |
| `JamSeshun.iOS/` | iOS entry point (stub)                                                                           |
| `JamSeshun.Tests/` | xUnit tests using `Avalonia.Headless.XUnit`                                                      |

## Architecture

**MVVM with DI.** `Microsoft.Extensions.DependencyInjection` wires views, view models, and services. The composition root is in `ServiceRegistry.cs`:
- `WithCommonServices()` — shared services (e.g. `GuitarTabsService`)
- `WithViewsAndViewModels()` — registers all VMs and keyed view controls
- Each platform entry point calls `AppBuilder.ConfigureServices(...)` to add its platform-specific `ITuningService` implementation

**ViewLocator** (`Services/ViewLocator.cs`) resolves views from view models by name convention (`*ViewModel` → `*View`), first via the keyed DI container, then by reflection. Register new view/VM pairs in `ServiceRegistry.WithViewsAndViewModels()`.

**Pitch detection pipeline:**
1. `ITuningService` (platform-specific) provides `IObservable<DetectedPitch>` from the microphone
2. `AutoCorrelationPitchDetector` finds the fundamental frequency via time-domain autocorrelation (robust at guitar's low frequencies where FFT bin width exceeds a semitone; locks onto the fundamental period even when harmonics dominate). `FftAlgorithm` remains for spectrogram rendering only
3. `TunerViewModel` subscribes, buffers over 300ms windows, and exposes `CurrentNote`, `CurrentFrequency`, `CurrentErrorInCents`
4. `TunerView` / `TunerNeedle` render the result

**Key dependencies:**
- `Avalonia` + `CommunityToolkit.Mvvm` — UI and MVVM bindings
- `System.Reactive` — all async audio data is modeled as `IObservable<T>`; globally imported in the shared project
- `SoundFlow` (Desktop only) — audio capture on Windows and Linux
- `MathNet.Numerics` — used in FFT processing
- `HtmlAgilityPack` + `Newtonsoft.Json` — used by `GuitarTabsService` for scraping/parsing tab data

## Code Style

Enforced via `.editorconfig` + `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` in `Directory.Build.props` — violations are **build errors**.

- **Braces required** on every `if`, `else`, `for`, `foreach`, `while`, `do` block, even single-line bodies.
- **`this.` required** on all instance member access (fields, properties, methods, events).
- **Private fields: camelCase, no `_` prefix.** e.g. `private int count;` not `private int _count;`
- **Constants: PascalCase.** e.g. `private const int MaxRetries = 3;`

> Naming violations (`IDE1006`) currently surface in the IDE only — `EnforceCodeStyleInBuild` does not yet catch them at the command line without `<AnalysisMode>All</AnalysisMode>`. Treat IDE squiggles as binding.

## Event Handling Policy

All event subscriptions in views and view models must follow these three rules:

**1. Rx observables with IDisposable cleanup.**
Never use bare `+=` subscriptions. Use `Observable.FromEventPattern`, `Observable.FromEvent`, or `Interactive.AsObservable` (see `Services/RoutedEventExtensions.cs`), and store the returned `IDisposable` in a `CompositeDisposable`. Views dispose in `OnDetachedFromVisualTree`; view models dispose in `Dispose()`.
Never declare `event` on view models or services, instead define a `Subject<T>` or `BehaviourSubject<T>` to allow clean multi-subscription, time shifting, buffering and enforced disposal.

```csharp
// CLR event (e.g. Button.Click)
this.disposables.Add(
    Observable.FromEventPattern<RoutedEventArgs>(h => this.MyButton.Click += h, h => this.MyButton.Click -= h)
        .Subscribe(_ => this.HandleClick())
);

// Routed event with AddHandler/RemoveHandler
this.disposables.Add(
    this.myControl.AsObservable(PointerPressedEvent, RoutingStrategies.Bubble)
        .Subscribe(e => this.HandlePointer(e))
);

// IObservable<T> on a view model
this.disposables.Add(vm.Saved.Subscribe(_ => this.HandleSaved()));
```

**2. No data loading in constructors.**
Constructors must only assign fields and wire commands. All I/O, DB reads, and service calls must be triggered explicitly:
- Views: load inside `OnAttachedToVisualTree` or a method called from it.
- View models: expose a `Load()` or `LoadAsync()` method; the view calls it on attach.

**3. I/O and DB reads off the UI thread.**
Wrap all `LiteDB`/file reads in `Task.Run(...)`. After the background work completes, marshal UI updates back via `ObserveOn(AvaloniaScheduler.Instance)` or the default `await` continuation (which resumes on the UI `SynchronizationContext`).

```csharp
// ViewModel: Changed subscription fetches off UI thread, applies on UI thread
this.library.Changed
    .SelectMany(_ => Observable.FromAsync(async ct =>
        await Task.Run(() => this.library.GetAll().ToList(), ct)))
    .ObserveOn(AvaloniaScheduler.Instance)
    .Subscribe(this.ApplyResults);

// ViewModel: async load method
public async Task LoadAllAsync()
{
    var data = await Task.Run(() => this.library.GetAll().ToList());
    this.ApplyResults(data); // called on UI thread via await continuation
}
```

## Notes

- `Directory.Build.props` sets `AvaloniaVersion` globally — update it there for all Avalonia package bumps
- `Avalonia.Diagnostics` was removed in Avalonia 12; replaced by `AvaloniaUI.DiagnosticsSupport` (Debug-only, same conditional pattern)
- `AvaloniaUseCompiledBindingsByDefault` is enabled — keep this when adding new bindings; `{Binding}` in XAML is now compiled by default
- The `BindingPlugins.DataValidators` workaround from Avalonia 11 was removed; data annotation validation is disabled by default in Avalonia 12
- Android: `AvaloniaMainActivity` is now non-generic; app customization (services, fonts) moved to `AvaloniaAndroidApplication<App>` subclass in `MainActivity.cs`
- iOS project can only be built on macOS; `dotnet restore`/`build` from Linux should target other projects individually
- Solution is now `.slnx` format (generated via `dotnet solution migrate`); the `.NET 10 SDK` (`~/.dotnet/dotnet`) is installed at `~/.dotnet`
