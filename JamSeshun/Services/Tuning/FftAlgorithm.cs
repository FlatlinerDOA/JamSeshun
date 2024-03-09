namespace JamSeshun.Services.Tuning;

using System.Buffers;
using System.Numerics;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;

/// <summary>
/// Cooley-Tukey FFT algorithm.
/// </summary>
public static class FftAlgorithm
{
    private static readonly Complex[] Zeroes = new Complex[512];
    /// <summary>
    /// Calculates FFT using Cooley-Tukey FFT algorithm.
    /// </summary>
    /// <param name="x">input data</param>
    /// <returns>spectrogram of the data</returns>
    /// <remarks>
    /// If amount of data items not equal a power of 2, then algorithm
    /// automatically pad with 0s to the lowest amount of power of 2.
    /// </remarks>
    public static ReadOnlySpan<double> Calculate(ReadOnlySpan<float> samples)
    {
        int length;
        int bitsInLength;
        if (IsPowerOfTwo(samples.Length))
        {
            length = samples.Length;
            bitsInLength = Log2(length) - 1;
        }
        else
        {
            bitsInLength = Log2(samples.Length);
            length = 1 << bitsInLength;
            // the items will be pad with zeros
        }

        // AC: We must zero the complex array every time we rent it as previous calls may have left it dirty.
        var buffer = ArrayPool<Complex>.Shared.Rent(length);
        var data = buffer.AsSpan().Slice(0, length);
        for (int i = 0; i < data.Length; i+= Zeroes.Length)
        {
            Zeroes.AsSpan().CopyTo(data[i..(i + Zeroes.Length)]);
            //// Ensure zeroed.
            //data[i] = Complex.Zero;
        }

        // bit reversal
        for (int i = 0; i < samples.Length; i++)
        {
            int j = ReverseBits(i, bitsInLength);
            data[j] = new Complex((double)samples[i], 0);
        }


        // Cooley-Tukey 
        for (int i = 0; i < bitsInLength; i++)
        {
            int m = 1 << i; // 2^i
            int n = m * 2;
            double alpha = -(2 * Math.PI / n);

            for (int k = 0; k < m; k++)
            {
                // e^(-2*pi/N*k)
                Complex oddPartMultiplier = new Complex(0, alpha * k).PoweredE();

                for (int j = k; j < length; j += n)
                {
                    Complex evenPart = data[j];
                    Complex oddPart = oddPartMultiplier * data[j + m];
                    data[j] = evenPart + oddPart;
                    data[j + m] = evenPart - oddPart;
                }
            }
        }

        // calculate spectrogram
        double[] spectrogram = new double[length];
        for (int i = 0; i < spectrogram.Length; i++)
        {
            spectrogram[i] = data[i].AbsPower2();
        }

        ArrayPool<Complex>.Shared.Return(buffer);
        return spectrogram;
    }

    public static double[,] GenerateSpectrogramData(ReadOnlySpan<float> samples, int fftSize = 2042)
    {
        //int numWindows = (samples.Length - fftSize) / hopSize + 1;
        //double[,] spectrogram = new double[numWindows, fftSize / 2 + 1];

        //for (int i = 0; i < numWindows; i++)
        //{
        //    int start = i * hopSize;
        //    double[] windowedFrame = ApplyWindow(audioSamples, start, windowSize, windowType);

        //}

        int overlap = (fftSize / 2) + 1;
        int stepSize = fftSize - overlap;
        int totalFragments = (samples.Length - fftSize) / stepSize + 1;
        double[,] spectrogram = new double[overlap, totalFragments];

        for (int i = 0; i < totalFragments; i++)
        {
            Complex[] segment = new Complex[fftSize];
            for (int j = 0; j < fftSize; j++)
            {
                if (i * stepSize + j < samples.Length)
                {
                    segment[j] = new Complex(samples[i * stepSize + j], 0);
                }
            }

            Fourier.Forward(segment, FourierOptions.Matlab);

            for (var t = overlap - 1; t >= 0; t--)
            {
                spectrogram[t, i] = segment[t].Magnitude;
            }
        }

        return spectrogram;
    }

    private static double[] ApplyWindow(double[] signalFrame, int start, int windowSize, WindowFunction windowType)
    {
        double[] windowedFrame = new double[windowSize];

        switch (windowType)
        {
            case WindowFunction.Hamming:
                var hammingWindow = MathNet.Numerics.Window.Hamming(windowSize);
                for (int i = 0; i < windowSize; i++)
                {
                    windowedFrame[i] = signalFrame[start + i] * hammingWindow[i];
                }
                break;
            // Add more cases for other window functions below ...

            default:
                // Rectangular window (no change)
                Array.Copy(signalFrame, start, windowedFrame, 0, windowSize);
                break;
        }

        return windowedFrame;
    }

    public static WriteableBitmap RenderSpectrogramToBitmap(double[,] spectrogramData)
    {
        int width = spectrogramData.GetLength(1); // Time dimension
        int height = spectrogramData.GetLength(0); // Frequency dimension

        //var writableBitmap = new WriteableBitmap(new PixelSize(width, height), new Avalonia.Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);

        //using (var fb = writableBitmap.Lock())
        //{
        //    for (int x = 0; x < width; x++)
        //    {
        //        for (int y = 0; y < height; y++)
        //        {
        //            double magnitude = spectrogramData[x][y];
        //            byte intensity = (byte)(magnitude / spectrogramData.Max(m => m.Max()) * 255);
        //            var color = (uint)(255 << 24 | intensity << 16 | intensity << 8 | intensity); // ARGB format

        //            unsafe
        //            {
        //                uint* ptr = (uint*)fb.Address;
        //                ptr[y * fb.RowBytes / 4 + x] = color;
        //            }
        //        }
        //    }
        //}

        //return writableBitmap;

        WriteableBitmap bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormat.Rgba8888);
        
        using (ILockedFramebuffer buffer = bitmap.Lock())
        {
            unsafe
            {
                double max = 0;
                byte* bufferPtr = (byte*)buffer.Address;
                for (int y = 0; y < height; y++)
                {
                    max = spectrogramData.MaxSecondDimension(y, max);
                }
                
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double magnitude = spectrogramData[y, x];
                        var intensity = magnitude / max;
                        byte colorValue = MapIntensityToColor(intensity);

                        // Set RGBA (example - grayscale)
                        *bufferPtr++ = colorValue;
                        *bufferPtr++ = colorValue;
                        *bufferPtr++ = colorValue;
                        *bufferPtr++ = 255; // Alpha
                    }
                }
            }
        }

        return bitmap;
    }

    private static byte MapIntensityToColor(double intensity)
    {
        // Example: Linear mapping 0.0 -> 0, 1.0 -> 255
        return (byte)(intensity * 255);
    }

    /// <summary>
    /// Gets number of significant bytes.
    /// </summary>
    /// <param name="n">Number</param>
    /// <returns>Amount of minimal bits to store the number.</returns>
    private static int Log2(int n)
    {
        int i = 0;
        while (n > 0)
        {
            ++i;
            n >>= 1;
        }

        return i;
    }

    /// <summary>
    /// Reverses bits in the number.
    /// </summary>
    /// <param name="n">Number</param>
    /// <param name="bitsCount">Significant bits in the number.</param>
    /// <returns>Reversed binary number.</returns>
    private static int ReverseBits(int n, int bitsCount)
    {
        int reversed = 0;
        for (int i = 0; i < bitsCount; i++)
        {
            int nextBit = n & 1;
            n >>= 1;

            reversed <<= 1;
            reversed |= nextBit;
        }
        return reversed;
    }

    private static uint ReverseBits(uint n)
    {
        n = (n >> 16) | (n << 16);
        n = ((n & 0xff00ff00) >> 8) | ((n & 0x00ff00ff) << 8);
        n = ((n & 0xf0f0f0f0) >> 4) | ((n & 0x0f0f0f0f) << 4);
        n = ((n & 0xcccccccc) >> 2) | ((n & 0x33333333) << 2);
        n = ((n & 0xaaaaaaaa) >> 1) | ((n & 0x55555555) << 1);
        return n;
    }

    /// <summary>
    /// Checks if number is power of 2.
    /// </summary>
    /// <param name="n">number</param>
    /// <returns>true if n=2^k and k is positive integer</returns>
    private static bool IsPowerOfTwo(int n) => n > 1 && (n & (n - 1)) == 0;
}
