using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Desktop;

/// <summary>
/// Placeholder until a cross-platform audio backend (e.g. PortAudio) is added.
/// Returns a single dummy device so the UI can be exercised on Linux/WSL2.
/// </summary>
internal class LinuxTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
        Observable.Return<IReadOnlyList<AudioCaptureDevice>>(
            [new AudioCaptureDevice(0, "Default (Linux — audio not yet supported)", IsDefault: true)]);

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device) =>
        Observable.Never<DetectedPitch>();
}
