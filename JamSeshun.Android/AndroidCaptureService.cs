using Android.Media;
using JamSeshun.Services.Tuning;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace JamSeshun.Android;

using System.Collections.Concurrent;
using System.Threading;

internal sealed class AndroidAudioCaptureService : IAudioCaptureService, IDisposable
{
    private const int CaptureSampleRate = 44100;
    private const ChannelIn ChannelConfig = ChannelIn.Mono;
    private const Encoding AudioEncoding = Encoding.PcmFloat; // Matches F32 format (API 21+)

    private readonly ConcurrentDictionary<int, IObservable<AudioFrame>> sharedStreams = new();
    private readonly Lock gate = new();
    private bool isDisposed;

    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices()
    {
        // Maps directly to Android system microphone input source routing
        var defaultMic = new AudioCaptureDevice(
            (int)AudioSource.Mic,
            "Built-in Microphone",
            true
        );

        return Observable.Return<IReadOnlyList<AudioCaptureDevice>>(new List<AudioCaptureDevice> { defaultMic });
    }

    public IObservable<AudioFrame> Capture(AudioCaptureDevice device)
    {
        lock (this.gate)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(AndroidAudioCaptureService));
            }
        }

        return this.sharedStreams.GetOrAdd(device.Id, id => this.CreateCaptureStream(device.Id).Publish().RefCount());
    }

    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.isDisposed)
            {
                return;
            }
            this.isDisposed = true;
            this.sharedStreams.Clear();
        }
    }

    private IObservable<AudioFrame> CreateCaptureStream(int deviceId)
    {
        return Observable.Create<AudioFrame>(observer =>
        {
            // Emulates your original single background execution thread loop structure
            var scheduler = new EventLoopScheduler();
            var subscription = new CompositeDisposable(scheduler);

            subscription.Add(scheduler.Schedule(deviceId, (_, id) =>
            {
                int bufferSizeInBytes = AudioRecord.GetMinBufferSize(
                    CaptureSampleRate,
                    ChannelConfig,
                    AudioEncoding
                );

                if (bufferSizeInBytes <= 0)
                {
                    observer.OnError(new InvalidOperationException("Invalid audio hardware configuration."));
                    return Disposable.Empty;
                }

                // Initialise the native Android AudioRecord device
                var audioRecord = new AudioRecord(
                    (AudioSource)id,
                    CaptureSampleRate,
                    ChannelConfig,
                    AudioEncoding,
                    bufferSizeInBytes
                );

                if (audioRecord.State != State.Initialized)
                {
                    observer.OnError(new InvalidOperationException("Failed to initialize Android AudioRecord component."));
                    audioRecord.Release();
                    return Disposable.Empty;
                }

                // Registers clean up execution upon unsubscription tracking
                var hardwareCleanup = Disposable.Create(() =>
                {
                    try
                    {
                        if (audioRecord.RecordingState == RecordState.Recording)
                        {
                            audioRecord.Stop();
                        }
                    }
                    catch (Exception)
                    {
                        // Suppress cross-thread structural stop errors
                    }
                    finally
                    {
                        audioRecord.Release();
                        audioRecord.Dispose();
                    }
                });

                try
                {
                    audioRecord.StartRecording();
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    hardwareCleanup.Dispose();
                    return Disposable.Empty;
                }

                // Setup 4096 element internal float buffer (~92ms buffer windows)
                float[] readBuffer = new float[4096];

                // Continuous processing streaming loop inside EventLoop background context
                while (!subscription.IsDisposed)
                {
                    int readResult = audioRecord.Read(
                        readBuffer,
                        0,
                        readBuffer.Length,
                        (int)AudioRecordReadOptions.Blocking
                    );

                    if (readResult > 0)
                    {
                        float[] frameSamples = new float[readResult];
                        Array.Copy(readBuffer, frameSamples, readResult);

                        observer.OnNext(new AudioFrame(frameSamples, CaptureSampleRate));
                    }
                    else if (readResult < 0)
                    {
                        var errorType = (TrackStatus)readResult;
                        if (!subscription.IsDisposed)
                        {
                            observer.OnError(new InvalidOperationException($"AudioRecord tracking error: {errorType}"));
                        }
                        break;
                    }
                }

                return hardwareCleanup;
            }));

            return subscription;
        });
    }
}
