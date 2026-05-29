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

| Project | Purpose |
|---|---|
| `JamSeshun/` | Shared UI library — views, view models, services (cross-platform) |
| `JamSeshun.Desktop/` | Desktop entry point; registers `WindowsTuningService` (NAudio) |
| `JamSeshun.Android/` | Android entry point; registers `AndroidTuningService` |
| `JamSeshun.iOS/` | iOS entry point (stub) |
| `JamSeshun.Tests/` | xUnit tests using `Avalonia.Headless.XUnit` |

## Architecture

**MVVM with DI.** `Microsoft.Extensions.DependencyInjection` wires views, view models, and services. The composition root is in `ServiceRegistry.cs`:
- `WithCommonServices()` — shared services (e.g. `GuitarTabsService`)
- `WithViewsAndViewModels()` — registers all VMs and keyed view controls
- Each platform entry point calls `AppBuilder.ConfigureServices(...)` to add its platform-specific `ITuningService` implementation

**ViewLocator** (`Services/ViewLocator.cs`) resolves views from view models by name convention (`*ViewModel` → `*View`), first via the keyed DI container, then by reflection. Register new view/VM pairs in `ServiceRegistry.WithViewsAndViewModels()`.

**Pitch detection pipeline:**
1. `ITuningService` (platform-specific) provides `IObservable<DetectedPitch>` from the microphone
2. `FftPitchDetector` applies FFT (`FftAlgorithm`) to find the fundamental frequency via harmonic peak analysis
3. `TunerViewModel` subscribes, buffers over 300ms windows, and exposes `CurrentNote`, `CurrentFrequency`, `CurrentErrorInCents`
4. `TunerView` / `TunerNeedle` render the result

**Key dependencies:**
- `Avalonia` + `CommunityToolkit.Mvvm` — UI and MVVM bindings
- `System.Reactive` — all async audio data is modeled as `IObservable<T>`; globally imported in the shared project
- `NAudio` (Desktop only) — audio capture on Windows
- `MathNet.Numerics` — used in FFT processing
- `HtmlAgilityPack` + `Newtonsoft.Json` — used by `GuitarTabsService` for scraping/parsing tab data

## Notes

- `Directory.Build.props` sets `AvaloniaVersion` globally — update it there for all Avalonia package bumps
- `Avalonia.Diagnostics` was removed in Avalonia 12; replaced by `AvaloniaUI.DiagnosticsSupport` (Debug-only, same conditional pattern)
- `AvaloniaUseCompiledBindingsByDefault` is enabled — keep this when adding new bindings; `{Binding}` in XAML is now compiled by default
- The `BindingPlugins.DataValidators` workaround from Avalonia 11 was removed; data annotation validation is disabled by default in Avalonia 12
- Android: `AvaloniaMainActivity` is now non-generic; app customization (services, fonts) moved to `AvaloniaAndroidApplication<App>` subclass in `MainActivity.cs`
- iOS project can only be built on macOS; `dotnet restore`/`build` from Linux should target other projects individually
- Solution is now `.slnx` format (generated via `dotnet solution migrate`); the `.NET 10 SDK` (`~/.dotnet/dotnet`) is installed at `~/.dotnet`
