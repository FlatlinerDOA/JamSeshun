using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Speech;
using System.Threading;
using Android.Runtime;
using Xamarin.Essentials;
using Observable = System.Reactive.Linq.Observable;

namespace JamSeshun.Android;

using Services.Tuning;

public sealed class AsrLyricTrackerService : ILyricTrackerService
{
    private readonly Context context;
    private readonly IAudioCaptureService captureService;
    private readonly object gate = new();
    private bool isDisposed;

    public AsrLyricTrackerService(Context context, IAudioCaptureService captureService)
    {
        this.context = context ?? Application.Context;
        this.captureService = captureService;
    }

    public IObservable<LyricTrackingUpdate> TrackLyrics(
        AudioCaptureDevice device,
        IReadOnlyList<string> lines,
        IObservable<int> position)
    {
        lock (this.gate)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsrLyricTrackerService));
            }
        }

        return System.Reactive.Linq.Observable.Create<LyricTrackingUpdate>(observer =>
        {
            var compositeDisposable = new CompositeDisposable();

            // Track the current user position (with thread safety via lock-free state or local volatile variable)
            // Initialized to -1, but will be overwritten immediately by the position observable emission.
            int currentLineIndex = -1;

            // Track recent speech hypothese phrases to look backward inside a time context window
            var rollingPhrases = new ConcurrentQueue<(DateTime Timestamp, string Text)>();

            // Clean up old phrases outside the 5-second window
            var windowPruner = Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    var cutoff = DateTime.UtcNow.AddSeconds(-5);
                    while (rollingPhrases.TryPeek(out var item) && item.Timestamp < cutoff)
                    {
                        rollingPhrases.TryDequeue(out var _);
                    }
                });
            compositeDisposable.Add(windowPruner);

            // Subscribe to position mutations (Prev/Next navigation override handling)
            var positionSubscription = position
                .Subscribe(pos =>
                {
                    Interlocked.Exchange(ref currentLineIndex, pos);
                    // Emit immediate initialization/reset signal for the current line state
                    observer.OnNext(new LyricTrackingUpdate(pos, 1.0f));
                });
            compositeDisposable.Add(positionSubscription);

            // Setup underlying Android hardware streaming components wrapped in Android Main Thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (compositeDisposable.IsDisposed)
                {
                    return;
                }

                SpeechRecognizer speechRecognizer = null;
                ParcelFileDescriptor[] pipeFDs = null;
                ParcelFileDescriptor.AutoCloseOutputStream pipeWriteStream = null;

                try
                {
                    speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(this.context);
                    var listener = new LyricTrackingRecognitionListener();
                    speechRecognizer.SetRecognitionListener(listener);

                    // Replicate internal VAD & continuous transcription stride by cycling the engine
                    // Android's SpeechRecognizer stops listening automatically after silence flags (OnEndOfSpeech).
                    // We catch that loop event to instantly restart listening without resetting user session data.
                    void RestartEngine()
                    {
                        if (compositeDisposable.IsDisposed || this.isDisposed)
                        {
                            return;
                        }

                        try
                        {
                            pipeWriteStream?.Close();
                        }
                        catch
                        { /* Suppress */
                        }
                        if (pipeFDs != null)
                        {
                            foreach (var fd in pipeFDs)
                            {
                                fd?.Dispose();
                            }
                        }

                        pipeFDs = ParcelFileDescriptor.CreatePipe();
                        pipeWriteStream = new ParcelFileDescriptor.AutoCloseOutputStream(pipeFDs[1]);

                        var recognizerIntent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                        recognizerIntent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                        recognizerIntent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
                        recognizerIntent.PutExtra(RecognizerIntent.ExtraAudioSource, pipeFDs[0]);

                        speechRecognizer.StartListening(recognizerIntent);
                    }

                    // Handle text hypothesis data processing chunks
                    Action<string, bool> handleHypothesis = (text, isPartial) =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            return;
                        }

                        // Add new phrase chunk to tracking history
                        rollingPhrases.Enqueue((DateTime.UtcNow, text));

                        // Build unified rolling window text block
                        var workingTextWindow = string.Join(" ", rollingPhrases.Select(p => p.Text));

                        // Evaluate forward matches matching constraints via LyricMatcher logic
                        int targetIdx = Interlocked.CompareExchange(ref currentLineIndex, 0, 0);
                        var update = this.MatchLyricsForward(workingTextWindow, lines, targetIdx);

                        if (update != null && update.LineIndex > targetIdx)
                        {
                            // Atomically step forward if validation succeeds
                            if (Interlocked.CompareExchange(ref currentLineIndex, update.LineIndex, targetIdx) == targetIdx)
                            {
                                observer.OnNext(update);
                            }
                        }
                    };

                    listener.PartialResultReceived += (string text) => handleHypothesis(text, true);
                    listener.FinalResultReceived += (string text) => handleHypothesis(text, false);

                    listener.EngineStoppedOrErrored += () =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (!compositeDisposable.IsDisposed)
                            {
                                RestartEngine();
                            }
                        });
                    };

                    // Begin original root hardware execution
                    RestartEngine();

                    // Read captured audio frame buffers from downstream services
                    var audioCaptureSubscription = this.captureService.Capture(device)
                        .ObserveOn(TaskPoolScheduler.Default)
                        .Subscribe(frame =>
                            {
                                if (pipeWriteStream == null)
                                {
                                    return;
                                }
                                try
                                {
                                    // Downsample F32 chunks to signed Int16 raw bytes required by Android Speech
                                    byte[] pcm16Bytes = this.ConvertFloatToPcm16Bytes(frame.Samples);
                                    pipeWriteStream.Write(pcm16Bytes, 0, pcm16Bytes.Length);
                                    pipeWriteStream.Flush();
                                }
                                catch (IOException)
                                { /* Pipe cycling boundary catches */
                                }
                                catch (Exception ex)
                                {
                                    observer.OnError(ex);
                                }
                            },
                            observer.OnError);

                    compositeDisposable.Add(audioCaptureSubscription);

                    // Add absolute hardware shutdown handlers
                    compositeDisposable.Add(Disposable.Create(() =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                speechRecognizer?.StopListening();
                                speechRecognizer?.Destroy();
                                speechRecognizer?.Dispose();
                            }
                            catch
                            { /* Suppress engine termination errors */
                            }

                            try
                            {
                                pipeWriteStream?.Close();
                            }
                            catch
                            {
                            }
                            if (pipeFDs != null)
                            {
                                foreach (var fd in pipeFDs)
                                {
                                    fd?.Dispose();
                                }
                            }
                        });
                    }));
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            });

            return compositeDisposable;
        });
    }

    private byte[] ConvertFloatToPcm16Bytes(float[] floatSamples)
    {
        byte[] pcm16Bytes = new byte[floatSamples.Length * 2];
        int byteIndex = 0;
        for (int i = 0; i < floatSamples.Length; i++)
        {
            float sample = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
            short shortVal = (short)(sample >= 0.0f ? sample * short.MaxValue : sample * short.MinValue * -1);
            pcm16Bytes[byteIndex++] = (byte)(shortVal & 0xFF);
            pcm16Bytes[byteIndex++] = (byte)((shortVal >> 8) & 0xFF);
        }
        return pcm16Bytes;
    }

    private LyricTrackingUpdate MatchLyricsForward(string transcribedWindow, IReadOnlyList<string> lines, int startingIndex)
    {
        if (startingIndex < 0 || startingIndex >= lines.Count)
        {
            return null;
        }

        // Clean out casing and punctuation properties for matching stability
        var transcribedTokens = this.Tokenize(transcribedWindow);
        if (transcribedTokens.Count == 0)
        {
            return null;
        }

        // Look forward through remaining lines (Max 3 lines deep hysteresis boundary check)
        int scanLimit = Math.Min(startingIndex + 4, lines.Count);
        for (int i = startingIndex + 1; i < scanLimit; i++)
        {
            string targetLine = lines[i];
            // Skip instrumental passages or empty entries as requested by interface spec
            if (string.IsNullOrWhiteSpace(targetLine))
            {
                continue;
            }
            var lineTokens = this.Tokenize(targetLine);
            if (lineTokens.Count == 0)
            {
                continue;
            }
            // Token overlap calculation
            int intersectionCount = lineTokens.Intersect(transcribedTokens).Count();
            float confidence = (float)intersectionCount / lineTokens.Count;
            // High token overlap matching criteria threshold (Hysteresis fallback value)
            if (confidence >= 0.45f)
            {
                return new LyricTrackingUpdate(i, confidence);
            }
        }
        return null;
    }
    private HashSet<string> Tokenize(string text)
    {
        return text.ToLowerInvariant().Split(new[]
        {
            ' ',
            ',',
            '.',
            '!',
            '?',
            '-',
            '\n',
            '\r'
        }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
    public void Dispose()
    {
        lock (this.gate)
        {
            this.isDisposed = true;
        }
    }
}

#region Native Event Proxy Listener Implementation

internal sealed class LyricTrackingRecognitionListener : Java.Lang.Object, IRecognitionListener
{
    public event Action<string> PartialResultReceived;
    public event Action<string> FinalResultReceived;
    public event Action EngineStoppedOrErrored;
    public void OnResults(Bundle results)
    {
        var text = this.GetTopMatch(results);
        if (text != null)
        {
            this.FinalResultReceived?.Invoke(text);
        }
        this.EngineStoppedOrErrored?.Invoke();

    }
    public void OnPartialResults(Bundle partialResults)
    {
        var text = this.GetTopMatch(partialResults);
        if (text != null)
        {
            this.PartialResultReceived?.Invoke(text);
        }
    }
    public void OnError([GeneratedEnum] SpeechRecognizerError error)
    {
        // Network timeouts or lack of audio input events flag tracking updates safely
        this.EngineStoppedOrErrored?.Invoke();
    }
    public void OnEndOfSpeech()
    {
        // Native VAD pipeline finished recording text slice tracking segment
        this.EngineStoppedOrErrored?.Invoke();
    }
    private string GetTopMatch(Bundle bundle)
    {
        var matches = bundle?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
        return (matches != null && matches.Count > 0) ? matches[0] : null;
    }

    public void OnReadyForSpeech(Bundle @params) { }

    public void OnBeginningOfSpeech() { }

    public void OnRmsChanged(float rmsdB) { }

    public void OnBufferReceived(byte[] buffer) { }

    public void OnEvent(int eventType, Bundle @params) { }
}

#endregion
