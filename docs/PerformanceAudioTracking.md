# Performance Mode — Audio-Based Line Progression

## Overview

Two complementary strategies can auto-advance the performance view as the user plays
and/or sings along.  They can run simultaneously via `Observable.Merge`; whichever fires
first advances the line.

| Strategy | Signal used | Best for |
|---|---|---|
| **Chord detection** | Guitar audio | Instrumentalists; always-on |
| **Lyric tracking (ASR)** | Vocal audio | Singers; optional download |

Both are **advance-only** and gated behind a user toggle, with manual Prev/Next always
available as an override.  Both expose a confidence value (see `ChordTrackingUpdate` and
`LyricTrackingUpdate` in `Services/Tuning/TrackingUpdate.cs`) so the UI can dim an
indicator at low confidence — the same pattern as the tuner's `DisplayOpacity`.

> **Key constraint:** The existing tuner pipeline captures at **44.1 kHz**.
> All ASR and chord-detection models below expect **16 kHz** input.
> An explicit **anti-aliased resampling stage** (44.1 k → 16 k) is required before any
> model inference and is not yet implemented.

---

## Strategy A — Chord Detection

### Model

**`alejogil35/distilhubert-finetuned-chorddetection`** (HuggingFace)
[https://huggingface.co/alejogil35/distilhubert-finetuned-chorddetection](https://huggingface.co/alejogil35/distilhubert-finetuned-chorddetection)

- Architecture: DistilHuBERT (6-layer distilled HuBERT-base, ~23 M params)
- Fine-tuned: chord classification from raw audio
- Input: 16 kHz mono PCM float32 (**requires resampling from 44.1 kHz**)
- Output: softmax over chord vocabulary (root × quality, e.g. C, Cm, G, Am, …)
- Size: ~25–50 MB INT8 ONNX — **estimated; must be verified on export**
- Latency: **unknown; must be measured per device.**
  DistilHuBERT is a transformer; on mid-range Android CPU/NNAPI, realistic latency
  for a 1-second frame is likely 100–300 ms, not the previously stated 20–60 ms.

### Export to ONNX

```bash
pip install optimum[exporters]
optimum-cli export onnx \
    --model alejogil35/distilhubert-finetuned-chorddetection \
    --task audio-classification \
    distilhubert-chord-onnx/
# Quantise to INT8:
python -m onnxruntime.quantization.preprocess --input distilhubert-chord-onnx/model.onnx
python -c "
from onnxruntime.quantization import quantize_dynamic, QuantType
quantize_dynamic('distilhubert-chord-onnx/model.onnx',
                 'distilhubert-chord-int8.onnx',
                 weight_type=QuantType.QInt8)
"
```

### Pipeline

```
Microphone (44.1 kHz)
    │
    ▼
Resample → 16 kHz mono PCM float32   ← NOT YET IMPLEMENTED
    │
    ▼
1-second rolling frame (16 000 samples)
    │
    ▼
DistilHuBERT chord classifier (ONNX)
    │  ChordTrackingUpdate(lineIndex, detectedChord, confidence)
    ▼
Advance logic
    │  if detectedChord ∈ chords_of_next_line AND confidence ≥ 0.6
    │  held for 2 consecutive frames → advance
    ▼
PerformanceViewModel.AdvanceTo(lineIndex)
```

### Advance Logic

Each `PerformanceLine` carries an ordered list of `Chord` records.  The chord tracker
compares detected labels against the **next** line's chord list with enharmonic
normalisation (e.g. `C#` ↔ `Db`):

```
next_line_chords = performanceLines[currentIndex + 1].Chords.Select(c => c.Name)
normalised_detected = normalise(detectedChord)

if normalised_detected ∈ normalise(next_line_chords) AND confidence ≥ 0.6:
    hold_count++
    if hold_count ≥ 2: advance()
else:
    hold_count = 0
```

### Service Interface

```csharp
// Returns ChordTrackingUpdate (lineIndex + detectedChord + confidence)
// so the UI can dim the tracking indicator at low confidence.
IObservable<ChordTrackingUpdate> TrackChords(
    AudioCaptureDevice device,
    IReadOnlyList<PerformanceLine> lines);
```

---

## Strategy B — Lyric Tracking (ASR)

### Model Options

> **Note on latency figures:** all numbers below are approximate and **must be measured**
> on target hardware before committing to a bundling strategy.

| Model | Size (INT8 ONNX) | Notes |
|---|---|---|
| **Whisper tiny** | multi-file, ~39 MB total | Default bundle. Encoder + decoder are separate ONNX graphs — not a single `.onnx` file. Reliable, multilingual. |
| Whisper base | multi-file, ~74 MB total | Better accuracy for mumbled/accented vocals. |
| **Qwen-Audio-0.6B** | ~350 MB+ | Autoregressive audio-LLM; best for sung/melodic input. **Not suitable for real-time 1-second stride on current mobile hardware.** Treat as research / future. |

Ship **Whisper tiny** as the default.  Whisper uses encoder–decoder autoregressive
decoding; per-window latency on mobile CPU is typically **several hundred ms to >1 s**,
not the previously stated 50 ms.  The 5-second rolling window with 1-second stride means
a new inference every second — verify this fits within the stride budget before shipping.

### Pipeline

```
Microphone (44.1 kHz)
    │
    ▼
Resample → 16 kHz mono PCM float32   ← NOT YET IMPLEMENTED
    │
    ▼
Silero VAD gate  (~2 MB ONNX)
    │  discard silent/guitar-only frames
    ▼
5-second rolling buffer, 1-second stride
    │
    ▼
Whisper tiny (ONNX — encoder + decoder graphs)
    │  transcribed text fragment
    ▼
Fuzzy lyric matcher
    │  token-overlap score vs [current, current+1, current+2] lyric lines
    ▼
LyricTrackingUpdate(lineIndex, confidence) → PerformanceViewModel.AdvanceTo
```

### Lyric Matching Algorithm

```
normalise(s) = lowercase(strip_punctuation(s)).split()

score(fragment, line) =
    |tokens(fragment) ∩ tokens(line)| / max(|tokens(line)|, 1)

advance when score(fragment, lines[i+1]) > 0.4,
held for 2 consecutive windows (hysteresis).
```

The `OR score(i) < 0.1 AND score(i+1) > score(i)` branch from an earlier draft is
**removed** — it fires spuriously on silence and contradicts the advance-only contract.

Lines with empty `LyricText` (instrument-only passages) are skipped by the lyric tracker;
the chord tracker handles progression through those lines.

### Service Interface

```csharp
// Returns LyricTrackingUpdate (lineIndex + confidence), advance-only.
// Never completes on its own — dispose the subscription to stop tracking.
IObservable<LyricTrackingUpdate> TrackLyrics(
    AudioCaptureDevice device,
    IReadOnlyList<string> lyricLines);
```

---

## Combined PerformanceViewModel Integration

`PerformanceViewModel` accepts both trackers as nullable optional dependencies.
`OnShow()` subscribes to whichever are non-null and enabled via `CompositeDisposable`:

```csharp
public IDisposable OnShow()
{
    var subscriptions = new CompositeDisposable();

    if (this.chordTracker != null && this.audioTrackingEnabled && this.trackingDevice != null)
    {
        subscriptions.Add(
            this.chordTracker
                .TrackChords(this.trackingDevice, this.performanceLines)
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(u => this.AdvanceTo(u.LineIndex)));
    }

    if (this.lyricTracker != null && this.audioTrackingEnabled && this.trackingDevice != null)
    {
        var lyrics = this.performanceLines.Select(l => l.LyricText).ToList();
        subscriptions.Add(
            this.lyricTracker
                .TrackLyrics(this.trackingDevice, lyrics)
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(u => this.AdvanceTo(u.LineIndex)));
    }

    return subscriptions;
}
```

> **Index space:** both trackers emit indices into the `PerformanceLine` list.
> The `AdvanceTo` guard (`lineIndex > currentIndex`) prevents backward movement even if
> a tracker misbehaves.  Ensure the `PerformanceLine` list used by the trackers is
> identical to the one the VM steps through (avoid off-by-one from separate parse calls).

---

## .NET / Avalonia Integration

### NuGet Packages

Execution providers are **not selected automatically** — each requires its own NuGet
package and an explicit `AppendExecutionProvider_*` call on the `SessionOptions`.
Each also ships its own native binary, adding to app download size.

```xml
<!-- Base runtime (CPU only) — all platforms -->
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.20.*" />

<!-- iOS CoreML (GPU/ANE) — add alongside base; conflict-free on iOS TFM -->
<!-- Call: options.AppendExecutionProvider_CoreML() -->
<PackageReference Include="Microsoft.ML.OnnxRuntime"
                  Condition="'$(TargetFramework)'=='net10.0-ios'"
                  Version="1.20.*" />

<!-- Windows DirectML — SEPARATE package; conflicts with base CPU package -->
<!-- Call: options.AppendExecutionProvider_DML() -->
<PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML"
                  Condition="'$(TargetFramework)'=='net10.0-windows'"
                  Version="1.20.*" />

<!-- Android NNAPI — via Extensions; call: options.AppendExecutionProvider_Nnapi() -->
<PackageReference Include="Microsoft.ML.OnnxRuntime.Extensions"
                  Condition="'$(TargetFramework)'=='net10.0-android'"
                  Version="0.12.*" />
```

> **CoreML and NNAPI fall-back warning:** both EPs frequently fall back to CPU for
> transformer attention ops with dynamic shapes.  Always benchmark with and without the
> EP before advertising hardware acceleration.

### Model Bundling

Whisper and DistilHuBERT export as **multiple ONNX graphs** (encoder, decoder,
decoder-with-past, plus tokenizer/processor artifacts) — not a single `.onnx` file.
Plan the asset layout accordingly:

```
{LocalApplicationData}/JamSeshun/Models/
    distilhubert-chord/
        model.onnx (or model_quantized.onnx)
    whisper-tiny/
        encoder_model.onnx
        decoder_model.onnx
        decoder_with_past_model.onnx
        tokenizer/  (vocab, merges, config)
    silero-vad/
        silero_vad.onnx
```

**Avoid embedding large models as app binary resources** — this bloats the binary,
doubles the disk footprint at extraction time, and triggers App Store / Play Store size
warnings.  Prefer download-on-demand for all models (even Whisper tiny) with a progress
UI and local cache check on startup.

### Cold-Start Latency

First inference includes ONNX session initialisation and EP graph compilation
(CoreML/NNAPI compile on first use and cache the compiled graph).  This can take several
seconds.  Warm up the session asynchronously in the background when the user opens the
performance view, before they enable audio tracking.

### Threading

All `OnnxRuntime.Run()` calls must happen **off the UI thread** (project rule 3: I/O
and compute off the UI thread).  The tracker services own their capture and inference
threads; they must `ObserveOn(AvaloniaScheduler.Instance)` before emitting to the VM.

---

## Open Questions / Future Work

- **Resampling implementation:** choose a resampling library (e.g. `NWaves`, or a simple
  polyphase filter in managed code) and decide whether it lives in the shared project or
  per-platform tuning service.
- **Shared capture source:** if the tuner and a tracker run simultaneously (e.g. user
  tunes then performs), both need mic access.  Some platforms use exclusive-mode capture.
  Consider a shared `AudioCaptureSession` that fans out to multiple consumers rather than
  opening the device twice.
- **Enharmonic normalisation:** define a canonical mapping shared between `ChordLibrary`,
  the chord tracker, and the tab parser (e.g. always prefer sharps; `Db` → `C#`).
- **Qwen-Audio-0.6B viability:** revisit when a quantised ONNX checkpoint is available
  and mobile latency can be measured; currently not a shippable option.
- **Battery/thermal:** continuous transformer inference every 1 s will drain battery and
  may trigger thermal throttling on sustained use.  Consider a duty cycle (inference
  every 2–3 s) or pausing inference when signal level is below a threshold.
- **Testing:** add a fake `IChordTrackerService` and `ILyricTrackerService` (emitting
  scripted `TrackingUpdate` sequences) to the test project so `PerformanceViewModel`
  auto-advance logic can be exercised without real models.
