# Performance Mode — Lyric Tracking via On-Device ASR

## Goal

Auto-advance the performance view to the next chord+lyric line as the user sings or
plays along, using on-device speech/audio recognition to match the microphone input
against the tab's lyric lines.

## Model Selection

| Model | Size (INT8 ONNX) | Latency (mobile) | Notes |
|---|---|---|---|
| **Qwen-Audio-0.6B** | ~350 MB | ~100–200 ms/chunk | Preferred — trained on music + speech, understands partial/sung phrases |
| Whisper tiny | ~39 MB | ~50 ms/chunk | Excellent fallback — well-tested ONNX export via `openai/whisper` |
| Silero STT v3 | ~25 MB | ~30 ms/chunk | Russian/English only; very fast; useful for VAD gating |

**Recommended approach:** ship Whisper tiny by default (small enough to bundle as an app
resource). Provide Qwen-Audio-0.6B as an optional download for users who want better
recognition of sung/hummed vocals with harmonic content.

## Architecture

```
Microphone
    │  16 kHz mono PCM float32  (same pipeline as tuner)
    ▼
VAD gate (Silero VAD or energy threshold)
    │  only forward frames above voiced-activity threshold
    ▼
Sliding buffer  (e.g. 5-second window, 1-second stride)
    │
    ▼
ONNX ASR model  (Qwen-Audio / Whisper tiny)
    │  recognised text fragment
    ▼
Lyric matcher
    │  fuzzy match against [current, current+1, current+2] lyric lines
    ▼
PerformanceViewModel.AdvanceTo(lineIndex)
```

### VAD Gate

Before feeding the ASR model, apply a Voice Activity Detector so audio frames without
singing or speaking are discarded. This reduces power consumption and avoids hallucinated
transcriptions on guitar noise alone.

Options:
- **Energy + zero-crossing rate:** trivially simple; works well when guitar and vocals
  are on separate channels (rarely the case).
- **Silero VAD** (ONNX, ~25 MB): robust even when guitar is present; runs per 512-sample
  frame at 16kHz. Recommended.

### ASR Buffer Strategy

Use a rolling buffer of **5 seconds** advanced by **1 second** strides:
- Short enough to give near-real-time feedback.
- Long enough for Whisper/Qwen to have sufficient context.
- Overlap avoids missing a phrase that straddles a window boundary.

After each stride, trim the buffer to start just after the last detected word boundary
(Whisper returns word-level timestamps in its JSON output).

### Lyric Matching

Given a transcribed fragment `T` and a window of candidate lyric lines `L[i..i+2]`:

1. **Normalise** both sides: lowercase, strip punctuation, collapse whitespace.
2. **Token overlap score**: `|tokens(T) ∩ tokens(L[i])| / |tokens(L[i])|`
3. **Advance** when:
   - `score(L[i+1]) > 0.4` (next line has meaningful overlap), OR
   - `score(L[i]) < 0.1` AND `score(L[i+1]) > score(L[i])` (current line dropped off)
4. **Never go backwards** — only auto-advance, never auto-retreat.
5. **Hysteresis:** require the threshold to hold for two consecutive windows before
   advancing, to avoid false triggers on isolated word matches.

## .NET / Avalonia Integration

### NuGet Packages

```xml
<!-- Shared project -->
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.20.*" />

<!-- Platform overrides (faster execution via hardware delegates) -->
<!-- iOS: use CoreML EP -->
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.20.*"
                  Condition="'$(TargetFramework)'=='net10.0-ios'" />
<!-- Android: use NNAPI EP -->
<PackageReference Include="Microsoft.ML.OnnxRuntime.Extensions" Version="0.12.*"
                  Condition="'$(TargetFramework)'=='net10.0-android'" />
```

### Service Interface

```csharp
// JamSeshun/Services/Tuning/ILyricTrackerService.cs
public interface ILyricTrackerService : IDisposable
{
    /// <summary>
    /// Starts recognising audio from <paramref name="device"/> and emits the index
    /// of the lyric line (within <paramref name="lines"/>) that best matches
    /// the current audio window.  Only emits when the index changes.
    /// </summary>
    IObservable<int> TrackLyrics(AudioCaptureDevice device, IReadOnlyList<string> lines);
}
```

`PerformanceViewModel` injects `ILyricTrackerService?` (nullable — absent on platforms
where no model is bundled). When non-null and the user enables audio tracking, `OnShow()`
subscribes:

```csharp
public IDisposable OnShow()
{
    if (this.lyricTracker == null || this.selectedDevice == null)
    {
        return Disposable.Empty;
    }

    var lyricLines = this.performanceLines
        .Select(l => l.LyricText)
        .ToList();

    return this.lyricTracker
        .TrackLyrics(this.selectedDevice, lyricLines)
        .ObserveOn(AvaloniaScheduler.Instance)
        .Subscribe(this.AdvanceTo);
}
```

### Model Bundling

- Store the ONNX model file(s) in `Assets/Models/` as embedded resources in the shared
  `JamSeshun` project.
- On first run, extract to `LocalApplicationData` so the ONNX Runtime can memory-map
  the file (required on iOS; recommended on Android for performance).
- Provide a download-on-demand flow for the larger Qwen-Audio model so the base app
  stays small.

## Platform Notes

| Platform | ONNX EP | Notes |
|---|---|---|
| iOS 14+ | CoreML (float16) | Requires `OrtSessionOptionsAppendExecutionProvider_CoreML`. Supported in OnnxRuntime ≥1.16. |
| Android 8+ | NNAPI → CPU fallback | NNAPI available on most devices ≥Android 10. |
| Desktop (Windows) | DirectML → CPU fallback | DirectML provides GPU acceleration on Windows 10+. |

## Open Questions / Future Work

- **Sung vs spoken:** Qwen-Audio handles melodic/sung input better than Whisper. Evaluate
  whether the accuracy delta justifies the size penalty on first-time download.
- **Language support:** Tabs in non-English languages will need the correct Whisper model
  variant (or Qwen-Audio which is multilingual by default).
- **Chord noise rejection:** The guitar signal will be present in the mic alongside
  vocals. Evaluate whether a simple high-pass filter (vocals are ~80 Hz–8 kHz, guitar
  fundamentals start at ~80 Hz but harmonics overlap heavily) is sufficient, or whether
  source separation (Demucs tiny ONNX, ~50 MB) is needed.
- **Manual override:** Always allow manual Prev/Next tap regardless of audio tracking
  state. Audio tracking is a hint, not a lock.
- **Confidence display:** Show a subtle indicator in the UI when tracking is active and
  confident vs uncertain (similar to the tuner's `DisplayOpacity`).
