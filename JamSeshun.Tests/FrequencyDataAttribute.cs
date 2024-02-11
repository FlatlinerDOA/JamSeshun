using System.Reflection;
using Xunit.Sdk;

namespace JamSeshun.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FrequencyDataAttribute(double fundamental, int harmonicCount, double noise = 0.0d, double durationInSeconds = 1.0d, int sampleRate = 44100, double amplitude = 1.0d) : DataAttribute
    {
        public double Fundamental { get; } = fundamental;

        public int HarmonicCount { get; } = harmonicCount;

        public double Noise { get; } = noise;
        
        public int SampleRate { get; } = sampleRate;
        
        public double DurationInSeconds { get; } = durationInSeconds;

        public double Amplitude { get; } = amplitude;

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            yield return new object[]
            {
                new FrequencyExample(Fundamental, HarmonicCount, Noise, DurationInSeconds, SampleRate, Amplitude)
            };
        }
    }
}
