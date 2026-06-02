using JamSeshun.Services.Tuning;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace JamSeshun.Desktop;

internal class WindowsTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices()
    {
        var devices = new AudioCaptureDevice[WaveInEvent.DeviceCount];
        for (int i = 0; i < devices.Length; i++)
        {
            var cap = WaveInEvent.GetCapabilities(i);
            devices[i] = new AudioCaptureDevice(i, cap.ProductName, i == 0);
        }

        return Observable.Return(devices);
    }

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            IScheduler scheduler = Scheduler.CurrentThread;
            return scheduler.Schedule<int>(device.Id, (_, id) =>
            {
                var waveIn = new WaveInEvent
                {
                    DeviceNumber = id,
                    WaveFormat = new WaveFormat(44100, 1)
                };

                var buffered = new BufferedWaveProvider(waveIn.WaveFormat)
                {
                    DiscardOnBufferOverflow = true
                };
                var pitchDetector = new AutoCorrelationPitchDetector(waveIn.WaveFormat.SampleRate);
                var sampleProvider = new Wave16ToFloatProvider(buffered).ToSampleProvider();
                var bytesPerSample = waveIn.WaveFormat.BitsPerSample / 8;

                var bufferReady = Observable.FromEventPattern<WaveInEventArgs>(
                    h => waveIn.DataAvailable += h,
                    h => waveIn.DataAvailable -= h);

                var d = new CompositeDisposable
                {
                    bufferReady.Subscribe(e =>
                    {
                        buffered.AddSamples(e.EventArgs.Buffer, 0, e.EventArgs.BytesRecorded);

                        // Process every complete fixed-size window so the detector
                        // always gets consistent resolution. Draining each callback
                        // (rather than processing one window) keeps latency from
                        // accumulating when capture outpaces the window size.
                        var sampleBuffer = ArrayPool<float>.Shared.Rent(pitchDetector.SampleBufferSize);
                        try
                        {
                            while (buffered.BufferedBytes / bytesPerSample >= pitchDetector.SampleBufferSize)
                            {
                                var samplesRead = sampleProvider.Read(sampleBuffer, 0, pitchDetector.SampleBufferSize);
                                if (samplesRead != pitchDetector.SampleBufferSize)
                                {
                                    break;
                                }

                                // Emit every frame (pitched or not) so the UI can show
                                // the live signal level; the view model decides what to display.
                                var detected = pitchDetector.DetectPitch(sampleBuffer.AsSpan(0, samplesRead));
                                observer.OnNext(detected);
                            }
                        }
                        finally
                        {
                            ArrayPool<float>.Shared.Return(sampleBuffer, true);
                        }
                    }),
                    Disposable.Create(waveIn.StopRecording),
                    waveIn
                };

                waveIn.StartRecording();
                return d;
            });
        });
    }
}
