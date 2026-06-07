using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

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

        public override bool SupportsDiscoveryEnumeration() => true;

        public override ValueTask<IReadOnlyCollection<ITheoryDataRow?>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
        {
            IReadOnlyCollection<ITheoryDataRow?> rows =
            [
                new TheoryDataRow(new FrequencyExample(this.Fundamental, this.HarmonicCount, this.Noise, this.DurationInSeconds, this.SampleRate, this.Amplitude))
            ];
            return new ValueTask<IReadOnlyCollection<ITheoryDataRow?>>(rows);
        }
    }
}
