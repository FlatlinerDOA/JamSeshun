using JamSeshun.Services.Tuning;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace JamSeshun.Desktop;

internal class WindowsTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices()
    {
        var devices = new AudioCaptureDevice[WaveInEvent.DeviceCount];
        for (int i = 0; i < devices.Length; i++)
        {
            devices[i] = new AudioCaptureDevice(i, WaveInEvent.GetCapabilities(i).ProductName);
        }
        
        return Observable.Return(devices);
    }

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            IScheduler scheduler = Scheduler.CurrentThread;
            return scheduler.Schedule<int>(device.Id, (scheduler, id) =>
            {
                WaveInEvent waveIn = new WaveInEvent();
                waveIn.DeviceNumber = id;
                waveIn.WaveFormat = new WaveFormat(44100, 1);

                BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
                IWaveProvider waveProvider = new Wave16ToFloatProvider(bufferedWaveProvider);
                
                ////var pitchDetector = new BitStreamAutoCorrelatedPitchDetector(waveProvider.WaveFormat.SampleRate);
                var pitchDetector = new FftPitchDetector(waveProvider.WaveFormat.SampleRate); 
                var sampleProvider = waveProvider.ToSampleProvider();

                var bufferReady = Observable.FromEventPattern<WaveInEventArgs>(
                    h => waveIn.DataAvailable += h,
                    h => waveIn.DataAvailable -= h);
                var d = new CompositeDisposable
                {
                    bufferReady.Subscribe(e =>
                    {
                        if (bufferedWaveProvider != null)
                        {
                            bufferedWaveProvider.AddSamples(e.EventArgs.Buffer, 0, e.EventArgs.BytesRecorded);
                            bufferedWaveProvider.DiscardOnBufferOverflow = true;

                            var sampleBuffer = ArrayPool<float>.Shared.Rent(pitchDetector.SampleBufferSize);
                            var samplesRead = sampleProvider.Read(sampleBuffer, 0, pitchDetector.SampleBufferSize);
                            var detected = pitchDetector.DetectPitch(sampleBuffer.AsSpan()[..samplesRead]);
                            ArrayPool<float>.Shared.Return(sampleBuffer);

                            if (detected.Fundamental.Name != null)
                            {
                                observer.OnNext(detected);
                            }
                        }
                    }),
                    Disposable.Create(() =>
                    {
                        // stop recording
                        waveIn.StopRecording();
                    }),
                    waveIn
                };

                // begin record
                waveIn.StartRecording();
                return d;
            });
        });
    }

}
