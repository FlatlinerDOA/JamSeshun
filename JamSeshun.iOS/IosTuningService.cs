using AVFoundation;
using Foundation;
using JamSeshun.Services.Tuning;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace JamSeshun.iOS;

/// <summary>
/// iOS microphone capture using AVAudioEngine.
/// Feeds PCM float32 samples into <see cref="AutoCorrelationPitchDetector"/>.
///
/// AVAudioEngine installs a tap on the input node that delivers buffers on an
/// internal real-time audio thread.  All state shared between the tap callback
/// and the disposal path is accessed only from that single serial thread, so no
/// locking is required for the accumulation buffers.  Cancelling the
/// <see cref="CancellationTokenSource"/> on disposal guards the permission
/// callback and tap lambda against running after teardown.
///
/// <see cref="AVAudioPCMBuffer.FloatChannelData"/> is a <c>float**</c> at the
/// native level.  We peel the outer pointer with <see cref="Marshal.ReadIntPtr"/>
/// (no unsafe block required) and then copy samples with
/// <see cref="Marshal.Copy(IntPtr, float[], int, int)"/>.
/// </summary>
internal sealed class IosTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
        Observable.Return<IReadOnlyList<AudioCaptureDevice>>(
            [new AudioCaptureDevice(0, "Microphone", IsDefault: true)]);

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        return Observable.Create<DetectedPitch>(observer =>
        {
            var disposables = new CompositeDisposable();

            var cts = new CancellationTokenSource();

            // Cancel (not just dispose) so IsCancellationRequested becomes true
            // in both the permission callback and the tap lambda on teardown.
            disposables.Add(Disposable.Create(() =>
            {
                cts.Cancel();
                cts.Dispose();
            }));

            // RequestRecordPermission moved to AVAudioApplication in iOS 17.
            // Use a version guard so both paths are reachable and the compiler
            // does not warn about calling the deprecated overload unconditionally.
            // The granted-handler logic is identical in both branches.
            void OnPermissionResult(bool granted)
            {
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (!granted)
                {
                    observer.OnError(new UnauthorizedAccessException(
                        "Microphone permission was denied. " +
                        "Grant access in Settings → Privacy → Microphone to use the tuner."));
                    return;
                }

                StartEngine(observer, disposables, cts.Token);
            }

            if (OperatingSystem.IsIOSVersionAtLeast(17))
            {
                AVAudioApplication.RequestRecordPermission(OnPermissionResult);
            }
            else
            {
                AVAudioSession.SharedInstance().RequestRecordPermission(OnPermissionResult);
            }

            return disposables;
        });
    }

    private static void StartEngine(
        IObserver<DetectedPitch> observer,
        CompositeDisposable disposables,
        CancellationToken cancellationToken)
    {
        var audioSession = AVAudioSession.SharedInstance();

        // AVAudioSessionCategoryOptions has no zero-named member; cast 0 for "no options".
        audioSession.SetCategory(AVAudioSessionCategory.Record, (AVAudioSessionCategoryOptions)0, out NSError? categoryError);
        if (categoryError is not null)
        {
            observer.OnError(new InvalidOperationException(
                $"Could not configure audio session category: {categoryError.LocalizedDescription}"));
            return;
        }

        audioSession.SetActive(true, out NSError? activateError);
        if (activateError is not null)
        {
            observer.OnError(new InvalidOperationException(
                $"Could not activate audio session: {activateError.LocalizedDescription}"));
            return;
        }

        var engine = new AVAudioEngine();
        var inputNode = engine.InputNode;

        // Discover the hardware sample rate, then request a mono float32 tap.
        // AVAudioEngine converts from the hardware format automatically.
        var hardwareFormat = inputNode.GetBusInputFormat(0);
        int sampleRate = (int)hardwareFormat.SampleRate;

        var tapFormat = new AVAudioFormat(
            AVAudioCommonFormat.PCMFloat32,
            hardwareFormat.SampleRate,
            channels: 1,
            interleaved: false);

        var pitchDetector = new AutoCorrelationPitchDetector(sampleRate);
        int bufferSize = pitchDetector.SampleBufferSize;

        // sampleBuffer accumulates samples across callbacks until a full
        // pitchDetector window is ready.  scratchBuffer receives each raw
        // tap delivery via Marshal.Copy before accumulation.
        // Rent scratchBuffer generously: AVAudioEngine treats the tap buffer
        // size as a hint and may deliver more frames than requested.
        float[] sampleBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
        float[] scratchBuffer = ArrayPool<float>.Shared.Rent(bufferSize * 2);
        int writeIndex = 0;

        // Cleanup: stop the engine first (guarantees no further tap callbacks),
        // then remove the tap, dispose the engine, deactivate the session, and
        // return the rented arrays.
        disposables.Add(Disposable.Create(() =>
        {
            engine.Stop();
            inputNode.RemoveTapOnBus(0);
            engine.Dispose();
            AVAudioSession.SharedInstance().SetActive(false, out _);
            ArrayPool<float>.Shared.Return(sampleBuffer, clearArray: true);
            ArrayPool<float>.Shared.Return(scratchBuffer, clearArray: true);
        }));

        inputNode.InstallTapOnBus(0, (uint)bufferSize, tapFormat, (buffer, _) =>
        {
            // Wrap the entire callback body so that an unexpected exception
            // does not unwind into AVAudioEngine's native code and crash the app.
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Clamp to scratchBuffer length: the tap size is advisory and
                // AVAudioEngine may deliver more frames than requested.
                int frameCount = Math.Min((int)buffer.FrameLength, scratchBuffer.Length);
                if (frameCount <= 0)
                {
                    return;
                }

                // FloatChannelData is a float** at the native level.
                // Marshal.ReadIntPtr dereferences the outer pointer (reads the
                // address of channel 0's sample data) without an unsafe block.
                // Marshal.Copy then copies frameCount floats from that address
                // into the managed scratchBuffer.
                IntPtr channelDataPtr = buffer.FloatChannelData;
                IntPtr channel0Ptr = Marshal.ReadIntPtr(channelDataPtr);
                Marshal.Copy(channel0Ptr, scratchBuffer, 0, frameCount);

                // Accumulate into sampleBuffer, emitting a DetectedPitch each
                // time a full pitchDetector window is filled.
                ReadOnlySpan<float> incoming = scratchBuffer.AsSpan(0, frameCount);
                int incomingOffset = 0;

                while (incomingOffset < incoming.Length)
                {
                    int toCopy = Math.Min(bufferSize - writeIndex, incoming.Length - incomingOffset);
                    incoming.Slice(incomingOffset, toCopy)
                            .CopyTo(sampleBuffer.AsSpan(writeIndex, toCopy));
                    writeIndex += toCopy;
                    incomingOffset += toCopy;

                    if (writeIndex >= bufferSize)
                    {
                        DetectedPitch detected = pitchDetector.DetectPitch(
                            sampleBuffer.AsSpan(0, bufferSize));
                        observer.OnNext(detected);
                        writeIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });

        engine.StartAndReturnError(out NSError? startError);
        if (startError is not null)
        {
            observer.OnError(new InvalidOperationException(
                $"Could not start audio engine: {startError.LocalizedDescription}"));
        }
    }
}
