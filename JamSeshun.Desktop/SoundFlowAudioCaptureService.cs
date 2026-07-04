using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JamSeshun.Services.Tuning;
using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace JamSeshun.Desktop;

using System.Collections.Concurrent;

/// <summary>
/// Raw microphone capture for Windows/Linux via SoundFlow (MiniAudio backend).
/// Emits 44.1 kHz mono float32 <see cref="AudioFrame"/>s.  Streams are shared per
/// device (<c>Publish().RefCount()</c>) so the chord and lyric trackers listening at
/// the same time open the device once — see <see cref="IAudioCaptureService"/>.
/// </summary>
internal sealed class SoundFlowAudioCaptureService : IAudioCaptureService, IDisposable
{
    private const int CaptureSampleRate = 44100;

    private readonly MiniAudioEngine engine = new();
    private readonly ConcurrentDictionary<int, IObservable<AudioFrame>> sharedStreams = new();
    private readonly object gate = new();

    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
        Observable.Return<IReadOnlyList<AudioCaptureDevice>>(
            this.engine.CaptureDevices
                .Select(d => new AudioCaptureDevice((int)d.Id, d.Name, d.IsDefault))
                .ToList());

    public IObservable<AudioFrame> Capture(AudioCaptureDevice device) =>
        this.sharedStreams.GetOrAdd(device.Id, id => this.CreateCaptureStream(device.Id).Publish().RefCount());

    public void Dispose()
    {
        this.engine.Dispose();
    }

    private IObservable<AudioFrame> CreateCaptureStream(int deviceId)
    {
        return Observable.Create<AudioFrame>(observer =>
        {
            IScheduler scheduler = new EventLoopScheduler();
            var subscription = new CompositeDisposable(scheduler as IDisposable ?? Disposable.Empty);

            subscription.Add(scheduler.Schedule(deviceId, (_, id) =>
            {
                var format = new AudioFormat
                {
                    SampleRate = CaptureSampleRate,
                    Channels = 1,
                    Format = SampleFormat.F32,
                    Layout = ChannelLayout.Mono
                };

                var deviceInfo = this.engine.CaptureDevices.FirstOrDefault(d => (int)d.Id == id);
                var captureDevice = this.engine.InitializeCaptureDevice(deviceInfo, format);

                void OnAudioProcessed(Span<float> samples, Capability capability)
                {
                    observer.OnNext(new AudioFrame(samples.ToArray(), CaptureSampleRate));
                }

                captureDevice.OnAudioProcessed += OnAudioProcessed;
                captureDevice.Start();
                return new CompositeDisposable(
                    Disposable.Create(() =>
                    {
                        captureDevice.Stop();
                        captureDevice.OnAudioProcessed -= OnAudioProcessed;
                    }),
                    captureDevice);
            }));

            return subscription;
        });
    }
}
