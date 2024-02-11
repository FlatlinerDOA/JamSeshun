using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// Cooley-Tukey FFT algorithm.
/// </summary>
public static class FftAlgorithm
{
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

        // bit reversal
        var buffer = ArrayPool<Complex>.Shared.Rent(length);
        var data = buffer.AsSpan().Slice(0, length);
        //Complex[] data = new Complex[length];
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

    public static uint ReverseBits(uint n)
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