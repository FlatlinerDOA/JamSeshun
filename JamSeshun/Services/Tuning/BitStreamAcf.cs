using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.Services.Tuning
{
    /// <summary>
    /// The BistreamAcf class correlates a bit stream (stored in a BitArray) by
    /// itself shifted by position, pos.
    ///
    /// In standard ACF (autocorrelation function) the signal is multiplied by
    /// a shifted version of itself, delayed by position, pos, with the result
    /// accumulated for each point, half the window of interest. The higher the
    /// sum, the higher the periodicity.
    ///
    /// With bitstream auto correlation, the more efficient XOR operation is
    /// used instead. A single XOR operation works on N bits of an integer. We
    /// get a speedup factor of N (e.g. 64 for a 64 bit machine) compared to
    /// standard ACF, and not to mention that integer bit operations are a lot
    /// faster than floating point multiplications).
    ///
    /// With XOR you get a one when there’s a mismatch:
    ///
    ///    0 ^ 0 = 0
    ///    0 ^ 1 = 1
    ///    1 ^ 0 = 1
    ///    1 ^ 1 = 0
    ///
    /// After XOR, the number of bits (set to 1) is counted. The lower the
    /// count, the higher the periodicity. A count of zero gives perfect
    /// correlation: there is no mismatch.
    /// </summary>
    public sealed class BitStreamAcf
    {
        private BitArray bits;
        private int midArray;

        public BitStreamAcf(BitArray bits)
        {
            this.bits = new BitArray(bits); // Ensure a copy is made to prevent altering the original BitArray outside this class
            this.midArray = Math.Max(((bits.Length / sizeof(int) * 8) / 2) - 1, 1);
        }

        /// <summary>
        /// Gets the maximum position allowed to specified (this is half length of the bitstream so that it can be correlated with itself cleanly).
        /// </summary>
        public int MaximumPosition => this.midArray;

        /// <summary>
        /// Calculates an Auto Correlation of the bitstream with itself at the specified offset position.
        /// </summary>
        /// <param name="offset">The offset position.</param>
        /// <returns>A positive integer where lower numbers returned are more strongly correlated and zero is perfectly correlated.</returns>
        public int CalculateAcf(int offset)
        {
            if (offset < 0 || offset >= this.midArray)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Position must be within half the range of the bit array.");
            }

            // Create a shifted copy of the original bits and XOR it with itself.
            var xorResult = new BitArray(this.bits).LeftShift(offset).Xor(this.bits);

            // Count bits set to true in the result, as these represent decoherence
            int decoherence = 0;
            for (int i = offset; i < this.midArray; i++)
            {
                if (xorResult[i])
                {
                    decoherence++;
                }
            }

            return decoherence;
        }
    }
}
