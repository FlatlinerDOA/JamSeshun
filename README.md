# Jam Seshun

A personal guitar utility app for tuning and browsing guitar tabs. Share sheet music and perform together in sync.

## Current Status

### ✅ Implemented & Working
- **Guitar Tuner** — Real-time pitch detection via microphone with visual feedback (tuning needle, frequency display, cent error)
  - Autocorrelation-based pitch detection (robust at low frequencies)
  - Stabilized pitch output with 300ms buffers
  - Cross-platform support (Windows, Android)
  
- **Guitar Tab Browser & Viewer** — Browse and display guitar tabs
  - Fetch tabs from online sources
  - Parse and display in monospace font
  - Save tabs locally for offline access
  - View mode with proper formatting

- **Tab Editor** 
  - Basic text editor for importin.
  - Import a bunch of tab / chord files from your HDD, Phone, Google Drive, One Drive etc.

### 📋 Planned / Not Yet Started
- **Non-standard Tuning** — Tap a tune from a tab to start the Tuner with the custom tuning.
- **Performance Mode** — Perform a tab so that it is readable from a distance, chords + lyrics follow as you play.
- **Multi-user Collaboration** — Share tabs and perform together in sync
- **Network Features** — Find peers on same network or via share link
- **Chord Library Integration** — Interactive chord diagram display and learning
- **iOS Support** — Stub project exists, needs full implementation

## Platforms

| Platform | Status | Notes |
|----------|--------|-------|
| Windows | ✅ Working | Full feature support via NAudio for audio capture |
| Android | ✅ Working | Native audio capture; UI/input polish ongoing |
| Linux | ⚠️ Untested | Desktop project targets Linux but untested |
| iOS | 📋 Planned | Project stub exists; requires macOS to build |
| macOS | 📋 Planned | Desktop project should support it; untested |

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

## Architecture

**Stack:** .NET 10, Avalonia 12.0.4, xUnit, System.Reactive, MathNet.Numerics

**Pattern:** MVVM with Microsoft.Extensions.DependencyInjection

**Project Structure:**
- `JamSeshun/` — Shared UI library (views, view models, services)
- `JamSeshun.Desktop/` — Desktop entry point (Windows/Linux)
- `JamSeshun.Android/` — Android entry point
- `JamSeshun.iOS/` — iOS entry point (stub)
- `JamSeshun.Tests/` — xUnit tests (Avalonia.Headless.XUnit)

See [CLAUDE.md](CLAUDE.md) for detailed architecture and implementation notes.