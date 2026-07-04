# Jam Seshun

A personal guitar utility app for tuning and browsing guitar tabs.

## Current Status

### ✅ Implemented & Working

- **Guitar Tuner** — Real-time pitch detection via microphone with visual feedback
  - Autocorrelation-based pitch detection (robust at guitar's low frequencies)
  - Stabilised pitch output with confidence gating over 300 ms windows
  - Needle, tuning worm, per-string guide, and cent-error display
  - Tuning presets: Standard, Drop D, DADGAD, Open G/D, Eb Standard
  - Tap "· Tune" in any tab header to load that tab's tuning directly into the tuner

- **Guitar Tab Browser & Viewer**
  - Import `.txt` tab/chord files from local storage, phone, cloud drives
  - Parse and display tabs with syntax-highlighted chord lines, section headers, and tab notation
  - Save tabs locally via LiteDB for offline access
  - Full-text search across saved tabs
  - Chord strip with flyout diagrams for each chord found in the tab

- **Chord Library & Diagrams**
  - 200+ built-in chord fingerings (A–G, all common qualities and slash chords)
  - `ChordDiagramControl` renders fretboard diagrams from fret arrays
  - Tab-specific chord definitions parsed from the tab content override library fallbacks

- **Tab Editor**
  - Edit or create tabs with artist, song, tuning, capo, and content fields
  - Bulk import of multiple `.txt` files in one operation

- **Performance Mode** *(core implemented; audio tracking planned)*
  - Step through a tab's chord+lyric lines one at a time during practice or performance
  - Shows chord diagrams and lyric for each line, with upcoming lines previewed
  - Navigate manually with Prev/Next; guitar icon in tab header opens performance mode
  - Audio-based auto-advance is planned — see [`docs/PerformanceAudioTracking.md`](docs/PerformanceAudioTracking.md)

### 🔬 Planned / In Progress

- **Audio-Based Performance Auto-Advance** — Auto-step through lines as you play/sing:
  - Chord detection via `distilhubert-finetuned-chorddetection` (ONNX, Strategy A)
  - Lyric tracking via Whisper tiny ASR (ONNX, Strategy B)
  - Interfaces stubbed; requires 44.1 kHz → 16 kHz resampling stage before inference
  - See [`docs/PerformanceAudioTracking.md`](docs/PerformanceAudioTracking.md) for full design

- **Multi-user Collaboration** — Share tabs and perform together in sync
- **Network Features** — Find peers on the same network or via share link

## Platforms

| Platform | Status | Notes |
|----------|--------|-------|
| Windows | ✅ Working | Audio capture via SoundFlow (MiniAudio cross-platform backend) |
| Android | ✅ Working | Native audio capture via Android `AudioRecord` API |
| iOS | ✅ Builds; untested on device | Full `ITuningService` via `AVAudioEngine`; requires macOS to build. CI runs on `macos-26` via GitHub Actions |
| Linux | ⚠️ Untested | Desktop project uses SoundFlow which supports Linux; not validated |
| macOS | ⚠️ Untested | Desktop project should run; not validated |

## Build & Run

```bash
# Build everything (Android requires Android SDK; iOS requires macOS)
dotnet build

# Run desktop app (Windows/Linux)
dotnet run --project JamSeshun.Desktop

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~AutoCorrelationPitchDetectorSpecification"

# iOS build (macOS only, Apple Silicon CI runner)
dotnet build JamSeshun.iOS/JamSeshun.iOS.csproj -r iossimulator-arm64

# Android build (requires Android SDK)
dotnet build JamSeshun.Android
```

## Architecture

**Stack:** .NET 10, Avalonia 12, xUnit, System.Reactive, MathNet.Numerics, LiteDB

**Pattern:** MVVM with `Microsoft.Extensions.DependencyInjection`

**Project Structure:**

| Project | Purpose |
|---|---|
| `JamSeshun/` | Shared UI library — views, view models, services |
| `JamSeshun.Desktop/` | Desktop entry point (Windows/Linux) — SoundFlow audio |
| `JamSeshun.Android/` | Android entry point — native `AudioRecord` audio |
| `JamSeshun.iOS/` | iOS entry point — `AVAudioEngine` audio |
| `JamSeshun.Tests/` | xUnit headless UI and unit tests |

See [CLAUDE.md](CLAUDE.md) for detailed architecture, code style rules, and implementation notes.
See [docs/](docs/) for feature design documents.
