public record FrequencyExample(double Fundamental, int HarmonicCount, double Noise = 0.0d, double DurationInSeconds = 1.0d, int SampleRate = 44100, double Amplitude = 1.0d)
{
    private readonly Random random = new Random(42); // For noise generation

    public float[] Samples
    {
        get
        {
            int numSamples = (int)(this.DurationInSeconds * this.SampleRate);
            double[] wave = new double[numSamples];
            double timeIncrement = 1.0 / this.SampleRate;

            // Base sine wave
            for (int i = 0; i < numSamples; i++)
            {
                double time = i * timeIncrement;
                wave[i] = this.Amplitude * Math.Sin(2 * Math.PI * this.Fundamental * time);
            }

            // Add harmonics
            for (int n = 2; n <= this.HarmonicCount + 1; n++)
            {
                double harmonicAmplitude = this.Amplitude / n;
                for (int i = 0; i < numSamples; i++)
                {
                    double time = i * timeIncrement;
                    wave[i] += harmonicAmplitude * Math.Sin(2 * Math.PI * n * this.Fundamental * time);
                }
            }

            // Add noise
            if (this.Noise > 0)
            {
                for (int i = 0; i < numSamples; i++)
                {
                    wave[i] += this.Noise * (random.NextDouble() * 2 - 1); // Range [-noiseAmplitude, noiseAmplitude]
                }
            }

            return wave.Select(d => (float)d).ToArray();
        }
    }
}