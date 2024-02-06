using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace JamSeshun.Services.Tuning;

public static class MathExtensions
{
    public static int SmallestPow2(this int n)
    {
        int pow = 1;
        while (pow < n)
        {
            pow *= 2;
        }

        return pow;
    }

    /// <summary>
    /// e^c
    /// </summary>
    /// <param name="c">Complex number.</param>
    /// <returns>Complex number.</returns>
    public static Complex PoweredE(this Complex c)
    {
        double e = Math.Exp(c.Real);
        return new Complex(e * Math.Cos(c.Imaginary), e * Math.Sin(c.Imaginary));
    }

    public static double Power2(this Complex c) => c.Real * c.Real - c.Imaginary * c.Imaginary;

    /// <summary>
    /// Calculates the Real^2 + Imaginary^2
    /// </summary>
    /// <param name="c">Complex number</param>
    /// <returns>double precision real number.</returns>
    public static double AbsPower2(this Complex c) => c.Real * c.Real + c.Imaginary * c.Imaginary;

    public static void MultiplyInplace(Span<double> x, double y)
    {
        var vectors = MemoryMarshal.Cast<double, Vector<double>>(x);

        var count = 0;

        for (var i = 0; i < vectors.Length; i++)
        {
            vectors[i] *= y;
            count += Vector<double>.Count;
        }

        for (var i = count; i < x.Length; i++)
        {
            x[i] *= y;
        }
    }

    public static Span<Complex> AddSimd(this ReadOnlySpan<Complex> left, ReadOnlySpan<Complex> right)
    {
        if (Avx.IsSupported)
        {
            var result = new Complex[Math.Min(left.Length, right.Length)].AsSpan();
            var vectorRes = MemoryMarshal.Cast<Complex, Vector256<double>>(result);
            var vectorLeft = MemoryMarshal.Cast<Complex, Vector256<double>>(left);
            var vectorRight = MemoryMarshal.Cast<Complex, Vector256<double>>(right);
            for (int i = 0; i < vectorRes.Length; i++)
            {
                vectorRes[i] = Avx.Add(vectorLeft[i], vectorRight[i]);
            }

            for (int i = 2 * vectorRes.Length; i < result.Length; i++)
            {
                result[i] = left[i] + right[i];
            }

            return result;
        }
        else
        {
            return null;
        }
    }

    public static Span<Complex> SubtractSimd(this ReadOnlySpan<Complex> left, ReadOnlySpan<Complex> right)
    {
        var result = new Complex[Math.Min(left.Length, right.Length)].AsSpan();
        var vectorRes = MemoryMarshal.Cast<Complex, Vector256<double>>(result);
        var vectorLeft = MemoryMarshal.Cast<Complex, Vector256<double>>(left);
        var vectorRight = MemoryMarshal.Cast<Complex, Vector256<double>>(right);
        for (int i = 0; i < vectorRes.Length; i++)
            vectorRes[i] = Avx.Subtract(vectorLeft[i], vectorRight[i]);

        for (int i = 2 * vectorRes.Length; i < result.Length; i++)
            result[i] = left[i] - right[i];
        return result;
    }

    public static Span<Complex> MultiplySimd(this ReadOnlySpan<Complex> left, ReadOnlySpan<Complex> right)
    {
        var result = new Complex[Math.Min(left.Length, right.Length)].AsSpan();
        var vectorRes = MemoryMarshal.Cast<Complex, Vector256<double>>(result);
        var vectorLeft = MemoryMarshal.Cast<Complex, Vector256<double>>(left);
        var vectorRight = MemoryMarshal.Cast<Complex, Vector256<double>>(right);
        for (int i = 0; i < vectorRes.Length; i++)
        {
            var l = vectorLeft[i];
            var r = vectorRight[i];
            vectorRes[i] = Avx.HorizontalAdd(
                Avx.Multiply(
                    Avx.Multiply(l, r),
                    Vector256.Create(1.0, -1.0, 1.0, -1.0)),
                Avx.Multiply(
                    l,
                    Avx.Permute(r, 0b0101)
                    ));
        }
        for (int i = 2 * vectorRes.Length; i < result.Length; i++)
            result[i] = left[i] * right[i];
        return result;
    }
}