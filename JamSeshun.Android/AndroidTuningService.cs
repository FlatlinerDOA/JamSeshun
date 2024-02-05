using Android.Media;
using JamSeshun.Services.Tuning;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace JamSeshun.Android;

internal partial class AndroidTuningService : ITuningService
{
    public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices()
    {
        throw new NotImplementedException();
    }

    public IObservable<DetectedPitch> StartDetectingPitch(AudioCaptureDevice device)
    {
        throw new NotImplementedException();
    }
}
