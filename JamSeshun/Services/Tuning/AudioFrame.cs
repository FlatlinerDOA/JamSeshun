namespace JamSeshun.Services.Tuning;

/// <summary>
/// A block of mono PCM float32 samples delivered by an <see cref="IAudioCaptureService"/>.
/// </summary>
/// <param name="Samples">Mono samples in the range [-1, 1]. The array is owned by the receiver.</param>
/// <param name="SampleRate">Sample rate of <paramref name="Samples"/> in Hz (e.g. 44100).</param>
public sealed record AudioFrame(float[] Samples, int SampleRate);
