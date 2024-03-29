﻿using Avalonia.Controls.Shapes;
using JamSeshun.Services.Tuning;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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
                var f = new FileStream(@"C:\Temp\raw.pcm", FileMode.Create);
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
                            bufferedWaveProvider.DiscardOnBufferOverflow = false; ////true;
                            f.Write(e.EventArgs.Buffer, 0, e.EventArgs.BytesRecorded);
                            var samplesRecorded = bufferedWaveProvider.BufferedBytes / (waveIn.WaveFormat.BitsPerSample / 8);
                            var sampleBuffer = ArrayPool<float>.Shared.Rent(samplesRecorded);
                            var samplesRead = sampleProvider.Read(sampleBuffer, 0, samplesRecorded);
                            var detected = pitchDetector.DetectPitch(sampleBuffer.AsMemory().Span.Slice(0, samplesRead));
                            ArrayPool<float>.Shared.Return(sampleBuffer, true);
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
                        f.Flush();
                        f.Close();
                        using var fs = File.OpenRead(@"C:\Temp\raw.pcm");
                        var s = new RawSourceWaveStream(fs, waveIn.WaveFormat);
                        var outpath = @"C:\Temp\raw.wav";
                        WaveFileWriter.CreateWaveFile(outpath, s);
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
