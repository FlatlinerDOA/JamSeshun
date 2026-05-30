using System;
namespace JamSeshun.Services.Tuning;

public readonly record struct DetectedPitch(float EstimatedFrequency, Note Fundamental, float ErrorInCents, float SignalLevel = 0f);
