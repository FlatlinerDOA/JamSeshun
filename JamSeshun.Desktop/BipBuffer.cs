namespace JamSeshun.Desktop;

using System;

public sealed class BipBuffer<T> where T : unmanaged
{
    private readonly T[] data;
    private int aStart;
    private int aEnd;
    private int bEnd;
    private bool bInUse;
    // Allocated on first cross-boundary Poll; reused thereafter.
    private T[]? wrapBuffer;

    /// <summary>
    /// Initializes a new instance of the BipBuffer with a specific capacity.
    /// </summary>
    public BipBuffer(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentException("Size must be greater than zero.", nameof(size));
        }

        this.data = new T[size];
        this.Clear();
    }

    /// <summary>
    /// Resets the buffer to an empty state.
    /// </summary>
    public void Clear()
    {
        this.aStart = 0;
        this.aEnd = 0;
        this.bEnd = 0;
        this.bInUse = false;
    }

    /// <summary>
    /// Gets the total capacity of the buffer.
    /// </summary>
    public int Size => this.data.Length;

    /// <summary>
    /// Gets the total amount of allocated/used bytes across both regions.
    /// </summary>
    public int Used => (this.aEnd - this.aStart) + this.bEnd;

    /// <summary>
    /// Gets the amount of contiguous space currently available for writing.
    /// </summary>
    public int Unused
    {
        get
        {
            if (this.bInUse)
                /* distance between region B and region A */
            {
                return this.aStart - this.bEnd;
            }
            else
            {
                return this.data.Length - this.aEnd;
            }
        }
    }

    /// <summary>
    /// Returns true if there is no data in the buffer.
    /// </summary>
    public bool IsEmpty => this.aStart == this.aEnd;

    /// <summary>
    /// Checks whether region B should be activated.
    /// </summary>
    private void CheckForSwitchToB()
    {
        if (this.data.Length - this.aEnd < this.aStart - this.bEnd)
        {
            this.bInUse = true;
        }
    }

    /// <summary>
    /// Inserts data into the buffer if there is enough contiguous space.
    /// </summary>
    /// <param name="data">The data span to copy into the buffer.</param>
    /// <returns>The number of bytes added (0 if not enough space).</returns>
    public int Offer(ReadOnlySpan<T> data)
    {
        if (this.Unused < data.Length)
        {
            return 0;
        }

        if (this.bInUse)
        {
            data.CopyTo(this.data.AsSpan(this.bEnd));
            this.bEnd += data.Length;
        }
        else
        {
            data.CopyTo(this.data.AsSpan(this.aEnd));
            this.aEnd += data.Length;
        }

        this.CheckForSwitchToB();
        return data.Length;
    }

    /// <summary>
    /// Returns a look-ahead span of the data currently available to read from Region A.
    /// </summary>
    /// <param name="size">The expected size to peek.</param>
    /// <returns>A span containing the requested data, or an empty span if invalid.</returns>
    public ReadOnlySpan<T> Peek(int size)
    {
        if (this.data.Length < this.aStart + size)
        {
            return ReadOnlySpan<T>.Empty;
        }

        if (this.IsEmpty)
        {
            return ReadOnlySpan<T>.Empty;
        }

        return this.data.AsSpan(this.aStart, size);
    }

    /// <summary>
    /// Decommits/consumes <paramref name="size"/> elements and advances the read pointer.
    /// Returns empty if fewer than <paramref name="size"/> elements are buffered in total.
    /// When the requested range spans the A/B region boundary the elements are copied into
    /// a small internal wrap buffer (allocated once, reused) so the returned span is always
    /// contiguous. This copying only occurs during the rare wrap-around transition.
    /// </summary>
    public ReadOnlySpan<T> Poll(int size)
    {
        if (this.Used < size)
        {
            return ReadOnlySpan<T>.Empty;
        }

        int aAvail = this.aEnd - this.aStart;

        if (aAvail >= size)
        {
            // Fast path: all requested data is contiguous in region A.
            var result = this.data.AsSpan(this.aStart, size);
            this.aStart += size;
            this.MaybePromoteB();
            this.CheckForSwitchToB();
            return result;
        }

        // Slow path: data spans the A/B boundary — copy into wrap buffer.
        // aAvail < size, and Used >= size guarantees B has the remainder.
        if (this.wrapBuffer == null || this.wrapBuffer.Length < size)
        {
            this.wrapBuffer = new T[size];
        }

        this.data.AsSpan(this.aStart, aAvail).CopyTo(this.wrapBuffer);

        // Promote B to A, then copy the remainder from the start of the new A.
        this.aStart = 0;
        this.aEnd = this.bEnd;
        this.bEnd = 0;
        this.bInUse = false;

        int fromB = size - aAvail;
        this.data.AsSpan(0, fromB).CopyTo(this.wrapBuffer.AsSpan(aAvail));
        this.aStart = fromB;

        this.MaybePromoteB();
        this.CheckForSwitchToB();
        return this.wrapBuffer.AsSpan(0, size);
    }

    private void MaybePromoteB()
    {
        if (this.aStart == this.aEnd && this.bInUse)
        {
            this.aStart = 0;
            this.aEnd = this.bEnd;
            this.bEnd = 0;
            this.bInUse = false;
        }
    }
}
