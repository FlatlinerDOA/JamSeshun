namespace JamSeshun.Services.Tuning;

/// <summary>
/// A pitch reading after stabilization. <see cref="Note"/> has a null Name when
/// there is no confident pitch (silence / background noise). <see cref="Confidence"/>
/// is 0..1 and can drive UI prominence.
/// </summary>
public readonly record struct StabilizedPitch(Note Note, float Frequency, float ErrorInCents, float Confidence)
{
    public bool HasPitch => this.Note.Name != null;
}
