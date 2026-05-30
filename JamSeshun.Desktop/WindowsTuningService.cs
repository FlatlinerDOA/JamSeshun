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

                        // Only process when we have exactly the required number of samples.
                        // This ensures consistent FFT resolution on every call.
                        var samplesAvailable = buffered.BufferedBytes / bytesPerSample;
                        if (samplesAvailable < pitchDetector.SampleBufferSize) return;

                        var sampleBuffer = ArrayPool<float>.Shared.Rent(pitchDetector.SampleBufferSize);
                        try
                        {
                            var samplesRead = sampleProvider.Read(sampleBuffer, 0, pitchDetector.SampleBufferSize);
                            if (samplesRead != pitchDetector.SampleBufferSize) return;

                            var detected = pitchDetector.DetectPitch(sampleBuffer.AsSpan(0, samplesRead));
                            if (detected.Fundamental.Name != null)
                                observer.OnNext(detected);
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
