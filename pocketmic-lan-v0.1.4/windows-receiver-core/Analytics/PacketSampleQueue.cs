using System.Threading;

namespace PocketMicReceiver;

/// <summary>
/// One packet-level observation, sized to fit in a register pair and stay allocation-free at
/// the call site. Not every field applies to every <see cref="PacketOutcome"/>: unused fields
/// are left at their sentinel (see individual comments) rather than branching the struct shape,
/// because a fixed layout is what makes the queue below lock-free.
/// </summary>
internal readonly struct PacketSample
{
    public readonly long TimestampTicks;
    public readonly PacketOutcome Outcome;

    /// <summary>Gap size for <see cref="PacketOutcome.Lost"/>; otherwise 1.</summary>
    public readonly short Count;

    /// <summary>Jitter-buffer depth in ms at the time of the sample, or -1 when not applicable.</summary>
    public readonly short BufferMs;

    /// <summary>Input level in dBFS * 100, or <see cref="short.MinValue"/> when not applicable.</summary>
    public readonly short LevelCentiDbfs;

    public readonly bool GateActive;

    public PacketSample(
        long timestampTicks,
        PacketOutcome outcome,
        short count,
        short bufferMs,
        short levelCentiDbfs,
        bool gateActive)
    {
        TimestampTicks = timestampTicks;
        Outcome = outcome;
        Count = count;
        BufferMs = bufferMs;
        LevelCentiDbfs = levelCentiDbfs;
        GateActive = gateActive;
    }

    public const short NoBufferMs = -1;
    public const short NoLevel = short.MinValue;
}

/// <summary>
/// Fixed-capacity single-producer/single-consumer ring buffer, purpose-built for one job: get a
/// <see cref="PacketSample"/> off the audio receive thread without a lock, an allocation, or a
/// blocking wait.
///
/// Correctness rests on two rules the .NET memory model guarantees: a <see cref="Volatile.Write"/>
/// cannot be reordered before the plain write that precedes it (so the sample is fully written
/// into the backing array before the index that publishes it moves), and a matching
/// <see cref="Volatile.Read"/> on the other side observes that write once it observes the index
/// change. Both index fields are written by exactly one thread each — the producer only ever
/// writes <see cref="_head"/>, the consumer only ever writes <see cref="_tail"/> — which is what
/// makes this safe without a lock or an interlocked RMW.
///
/// On overflow, <see cref="TryEnqueue"/> drops the sample and returns false rather than
/// blocking. The audio thread must never wait on the analytics thread; losing a handful of
/// diagnostic samples during a stall is the correct trade, and the drop is itself counted so a
/// starved consumer is visible in the data rather than silently lying about it.
/// </summary>
internal sealed class PacketSampleQueue
{
    private readonly PacketSample[] _buffer;
    private readonly int _mask;
    private int _head;
    private int _tail;
    private long _dropped;

    public PacketSampleQueue(int capacity)
    {
        var size = 1;
        while (size < capacity) size <<= 1;
        _buffer = new PacketSample[size];
        _mask = size - 1;
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>Producer side. Called from the audio receive loop; must never block or allocate.</summary>
    public bool TryEnqueue(in PacketSample sample)
    {
        var head = _head;
        var next = (head + 1) & _mask;
        if (next == Volatile.Read(ref _tail))
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }

        _buffer[head] = sample;
        Volatile.Write(ref _head, next);
        return true;
    }

    /// <summary>Consumer side. Called only from the analytics background thread.</summary>
    public bool TryDequeue(out PacketSample sample)
    {
        var tail = _tail;
        if (tail == Volatile.Read(ref _head))
        {
            sample = default;
            return false;
        }

        sample = _buffer[tail];
        Volatile.Write(ref _tail, (tail + 1) & _mask);
        return true;
    }
}
