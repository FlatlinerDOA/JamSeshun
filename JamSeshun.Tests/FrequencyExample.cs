public record FrequencyExample(double Fundamental, int HarmonicCount, double Noise = 0.0d, double DurationInSeconds = 1.0d, int SampleRate = 44100, double Amplitude = 1.0d)
{
    // Custom amplitude per harmonic: index 0 = fundamental, 1 = 2f, 2 = 3f, ...
    // When set, overrides the default 1/n model.
    public double[]? HarmonicAmplitudes { get; init; }

    // Realistic guitar string: 2nd/3rd harmonics dominate the fundamental.
    // Amplitude ratios measured from acoustic guitar recordings.
    public static FrequencyExample GuitarString(double fundamental, double durationSeconds = 2.0, double noise = 0.05) =>
        new FrequencyExample(fundamental, 5, noise, durationSeconds)
        {
            HarmonicAmplitudes = [0.5, 1.0, 0.85, 0.5, 0.3, 0.15]
            //                    f    2f   3f    4f   5f   6f
        };

    private readonly Random random = new Random(42);

    public float[] Samples
    {
        get
        {
            int numSamples = (int)(this.DurationInSeconds * this.SampleRate);
            double[] wave = new double[numSamples];
            double dt = 1.0 / this.SampleRate;

            if (this.HarmonicAmplitudes is { } amps)
            {
                for (int h = 0; h < amps.Length; h++)
                {
                    double freq = this.Fundamental * (h + 1);
                    double amp = amps[h];
                    for (int i = 0; i < numSamples; i++)
                        wave[i] += amp * Math.Sin(2 * Math.PI * freq * i * dt);
                }
            }
            else
            {
                // Default: fundamental + harmonics at amplitude 1/n
                for (int i = 0; i < numSamples; i++)
                    wave[i] = this.Amplitude * Math.Sin(2 * Math.PI * this.Fundamental * i * dt);

                for (int n = 2; n <= this.HarmonicCount + 1; n++)
                {
                    double harmonicAmplitude = this.Amplitude / n;
                    for (int i = 0; i < numSamples; i++)
                        wave[i] += harmonicAmplitude * Math.Sin(2 * Math.PI * n * this.Fundamental * i * dt);
                }
            }

            if (this.Noise > 0)
            {
                for (int i = 0; i < numSamples; i++)
                    wave[i] += this.Noise * (this.random.NextDouble() * 2 - 1);
            }

            return wave.Select(d => (float)d).ToArray();
        }
    }
}
