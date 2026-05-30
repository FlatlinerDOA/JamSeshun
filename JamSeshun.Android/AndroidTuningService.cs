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
                var pitchDetector = new AutoCorrelationPitchDetector(s.SampleRate);
                var sampleBuffer = ArrayPool<float>.Shared.Rent(pitchDetector.SampleBufferSize);
                int writeIndex = 0;

                var d = new CompositeDisposable
                {
                    Disposable.Create(() => ArrayPool<float>.Shared.Return(sampleBuffer, true)),
                    s.AudioBuffers.Subscribe(audioBuffer =>
                    {
                        const int bytesPerSample = 2; // PCM16 = 2 bytes per sample
                        var byteSpan = audioBuffer.Span;
                        int byteOffset = 0;

                        while (byteOffset + bytesPerSample <= byteSpan.Length)
                        {
                            // PCM16 is a signed int16, normalize to [-1, 1]
                            sampleBuffer[writeIndex++] = BitConverter.ToInt16(byteSpan[byteOffset..]) / 32768f;
                            byteOffset += bytesPerSample;

                            if (writeIndex >= pitchDetector.SampleBufferSize)
                            {
                                var detected = pitchDetector.DetectPitch(sampleBuffer.AsSpan(0, pitchDetector.SampleBufferSize));
                                observer.OnNext(detected);
                                writeIndex = 0;
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
