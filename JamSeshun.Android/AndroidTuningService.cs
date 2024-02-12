using Android.Content.PM;
using Android.Media;
using JamSeshun.Services.Tuning;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace JamSeshun.Android;

internal partial class AndroidTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices()
    {
        return Observable.Return(new[]
        {
            new AudioCaptureDevice((int)AudioSource.Mic, "Microphone", true)
        });
    }

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            IScheduler scheduler = Scheduler.CurrentThread;
            return scheduler.ScheduleAsync<int>(device.Id, async (scheduler, c, id) =>
            {
                var s = new AudioStream();
                var pitchDetector = new FftPitchDetector(s.SampleRate);
                var sampleBuffer = ArrayPool<float>.Shared.Rent(pitchDetector.SampleBufferSize);
                var targetMemory = sampleBuffer.AsMemory();

                var d = new CompositeDisposable
                {
                    s.AudioBuffers.Subscribe(audioBuffer =>
                    {
                        var bytesPerSample = s.BitsPerSample / 8;
                        var samplesRead = audioBuffer.Length / bytesPerSample;
                        var byteSpan = audioBuffer.Span;
                        for (var i = 0; i < Math.Min(samplesRead, targetMemory.Length); i += bytesPerSample)
                        {
                            targetMemory.Span[i] = (float)BitConverter.ToHalf(byteSpan[..bytesPerSample]);
                            byteSpan = byteSpan[bytesPerSample..];
                            targetMemory = targetMemory[i..];
                        }

                        if (targetMemory.Length <= bytesPerSample)
                        {
                            var detected = pitchDetector.DetectPitch(sampleBuffer.AsMemory().Span);
                            ArrayPool<float>.Shared.Return(sampleBuffer, true);
                            sampleBuffer = ArrayPool<float>.Shared.Rent(pitchDetector.SampleBufferSize);
                            if (detected.Fundamental.Name != null)
                            {
                                observer.OnNext(detected);
                            }
                        }
                    })
                };

                await s.Start().ConfigureAwait(false);
                return d;
            });
        });
    }
}
