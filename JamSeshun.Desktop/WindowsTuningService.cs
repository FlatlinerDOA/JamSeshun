using JamSeshun.Services.Tuning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Threading;

namespace JamSeshun.Desktop;

internal class WindowsTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices()
    {
        var audioDevices = new MMDeviceEnumerator()
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new AudioCaptureDevice(d.ID, d.FriendlyName, d.DataFlow == DataFlow.Capture))
            .ToArray();
        return Observable.Return(audioDevices);
    }

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice captureDevice)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            IScheduler scheduler = ThreadPoolScheduler.Instance;
            return scheduler.Schedule<AudioCaptureDevice>(captureDevice, (IScheduler scheduler, AudioCaptureDevice dev) =>
            {
                var device = new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .Where(d => d.ID == dev.Id)
                    .Select(d => d.DataFlow switch { DataFlow.Render => new WasapiLoopbackCapture(d), _ => new WasapiCapture(d, true, 10) })
                    .FirstOrDefault();

                if (device == null)
                {
                    observer.OnError(new InvalidOperationException($"Device {dev.Name} not found"));
                    return Disposable.Empty;
                }

                device.ShareMode = AudioClientShareMode.Shared;
                int bytesPerSamplePerChannel = device.WaveFormat.BitsPerSample / 8;
                int bytesPerSample = bytesPerSamplePerChannel * device.WaveFormat.Channels;
                var bufferReady = Observable.FromEventPattern<WaveInEventArgs>(
                    h => device.DataAvailable += h,
                    h => device.DataAvailable -= h);


                ////WaveInEvent waveIn = new WaveInEvent();
                ////waveIn.DeviceNumber = device.Id;
                ////waveIn.WaveFormat = new WaveFormat(44100, 1);

                ////
                ////BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
                ////{
                ////    DiscardOnBufferOverflow = true,
                ////    BufferLength = pitchDetector.SampleBufferSize * (waveIn.WaveFormat.BitsPerSample / 8),
                ////};
                ////IWaveProvider waveProvider = new Wave16ToFloatProvider(bufferedWaveProvider);

                ////////var pitchDetector = new BitStreamAutoCorrelatedPitchDetector(waveProvider.WaveFormat.SampleRate);
                ////var sampleProvider = waveProvider.ToSampleProvider();

                //waveIn.RecordingStopped += (sender, e) =>
                //{
                //    if (e.Exception != null)
                //    {
                //        Console.WriteLine($"Recording stopped due to exception: {e.Exception.Message}");
                //    }
                //    else
                //    {
                //        Console.WriteLine("Recording stopped without exception.");
                //    }
                //};

                //var bufferStopped = Observable.FromEventPattern<StoppedEventArgs>(
                //   h => waveIn.RecordingStopped += h,
                //   h => waveIn.RecordingStopped -= h);

                //var bufferReady = Observable.FromEventPattern<WaveInEventArgs>(
                //    h => waveIn.DataAvailable += h,
                //    h => waveIn.DataAvailable -= h);
                var pitchDetector = new FftPitchDetector(device.WaveFormat.SampleRate);

                long concurrencyCount = 0;
                var d = new CompositeDisposable
                {
                    bufferReady.Subscribe(e =>
                    {
                        var count = Interlocked.Increment(ref concurrencyCount);
                        Debug.Assert(count == 1);
                        var maxLength = Math.Min(device.WaveFormat.SampleRate / 10, e.EventArgs.BytesRecorded);
                        int bufferSampleCount = Math.Min(e.EventArgs.Buffer.Length / bytesPerSample, maxLength);
                        var sampleBuffer = ArrayPool<float>.Shared.Rent(bufferSampleCount);
                        if (bytesPerSamplePerChannel == 2 && device.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
                        {
                            for (int i = 0; i < bufferSampleCount; i++)
                            {
                                sampleBuffer[i] = BitConverter.ToInt16(e.EventArgs.Buffer, i * bytesPerSample);
                            }
                        }
                        else if (bytesPerSamplePerChannel == 4 && device.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
                        {
                            for (int i = 0; i < bufferSampleCount; i++)
                            {
                                sampleBuffer[i] = BitConverter.ToInt32(e.EventArgs.Buffer, i * bytesPerSample);
                            }
                        }
                        else if (bytesPerSamplePerChannel == 4 && device.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                        {
                            for (int i = 0; i < bufferSampleCount; i++)
                            {
                                sampleBuffer[i] = BitConverter.ToSingle(e.EventArgs.Buffer, i * bytesPerSample);
                            }
                        }
                        else
                        {
                            observer.OnError(new NotSupportedException(device.WaveFormat.ToString()));
                        }

                        var detected = pitchDetector.DetectPitch(sampleBuffer.AsSpan()[..bufferSampleCount]);
                        if (detected.Fundamental.Name != null)
                        {
                            observer.OnNext(detected);
                        }

                        ArrayPool<float>.Shared.Return(sampleBuffer);

                        count = Interlocked.Decrement(ref concurrencyCount);
                        Debug.Assert(count == 0);

                        //if (bufferedWaveProvider != null)
                        //{
                        //    var s = Stopwatch.StartNew();
                        //    bufferedWaveProvider.AddSamples(e.EventArgs.Buffer, 0, e.EventArgs.BytesRecorded);
                        //    var sampleBuffer = ArrayPool<float>.Shared.Rent(bufferedWaveProvider.BufferLength);
                        //    var samplesRead = sampleProvider.Read(sampleBuffer, 0, bufferedWaveProvider.BufferLength);
                        //    bufferedWaveProvider.ClearBuffer();
                        //    if (samplesRead > 0)
                        //    {
                        //        

                        //        s.Stop();
                        //        Console.WriteLine($"{s.ElapsedMilliseconds} ms");
                        //        if (detected.Fundamental.Name != null)
                        //        {
                        //            observer.OnNext(detected);
                        //        }
                        //    }

                        //    ArrayPool<float>.Shared.Return(sampleBuffer);
                        //}
                    },
                    ex =>
                    {
                        Console.Error.WriteLine(ex.ToString());
                    }),
                    Disposable.Create(() =>
                    {
                        // stop recording
                        device.StopRecording();
                    })
                };

                device.StartRecording();

                //// begin record
                //waveIn.StartRecording();
                return d;
            });
        });
    }

}
