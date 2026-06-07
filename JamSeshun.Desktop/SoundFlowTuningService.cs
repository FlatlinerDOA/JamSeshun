using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Desktop;

using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Structs;
using SoundFlow.Utils;

/// <summary>
/// Placeholder until a cross-platform audio backend (e.g. PortAudio) is added.
/// Returns a single dummy device so the UI can be exercised on Linux/WSL2.
/// </summary>
internal class SoundFlowTuningService : ITuningService
{
    private readonly MiniAudioEngine engine;

    public SoundFlowTuningService()
    {
        this.engine = new MiniAudioEngine();
    }

    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() => Observable.Return<IReadOnlyList<AudioCaptureDevice>>(this.engine.CaptureDevices.Select(d => new AudioCaptureDevice((int)d.Id, d.Name, d.IsDefault))
            .ToList());

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            IScheduler scheduler = new EventLoopScheduler();
            return scheduler.Schedule(device.Id, (_, deviceId) =>
            {
                var format = new AudioFormat
                {
                    SampleRate = 44100,
                    Channels = 1,
                    Format = SampleFormat.F32,
                    Layout = ChannelLayout.Mono
                };

                var pitchDetector = new AutoCorrelationPitchDetector(format.SampleRate);
                var deviceInfo = this.engine.CaptureDevices.FirstOrDefault(d => (int)d.Id == deviceId);
                var captureDevice = this.engine.InitializeCaptureDevice(deviceInfo, format);

                var buffer = new BipBuffer<float>(pitchDetector.SampleBufferSize * 4);
                void OnBufferReady(Span<float> samples, Capability capability)
                {
                    var remaining = samples;
                    while (!remaining.IsEmpty)
                    {
                        var written = buffer.Offer(remaining);
                        remaining = remaining.Slice(written);
                        while (buffer.Used >= pitchDetector.SampleBufferSize)
                        {
                            var poll = buffer.Poll(pitchDetector.SampleBufferSize);
                            observer.OnNext(pitchDetector.DetectPitch(poll));
                        }
                    }
                }

                captureDevice.OnAudioProcessed += OnBufferReady;
                captureDevice.Start();
                return new CompositeDisposable(
                    Disposable.Create(() =>
                    {
                        captureDevice.Stop();
                        captureDevice.OnAudioProcessed -= OnBufferReady;
                    }),
                    captureDevice);
            });
        });
    }
}
