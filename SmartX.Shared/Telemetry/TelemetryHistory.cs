using System.Collections;

namespace SmartX.Shared.Telemetry;

/// <summary>
/// Bounded ring of recent packets for one sensor. Newest readings overwrite the oldest once the
/// capacity is full, so the live ingest path cannot grow without limit while still exposing the
/// history as a proper collection rather than a raw <see cref="List{T}"/>.
/// </summary>
public sealed class TelemetryHistory<T> : IReadOnlyList<TelemetryPacket<T>>
    where T : struct
{
    public const int DefaultCapacity = 256;

    private readonly TelemetryPacket<T>?[] _slots;
    private int _oldest;
    private int _count;

    public TelemetryHistory(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Telemetry history must hold at least one packet.");
        }

        _slots = new TelemetryPacket<T>?[capacity];
    }

    public int Capacity => _slots.Length;

    public int Count => _count;

    public TelemetryPacket<T> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _slots[(_oldest + index) % _slots.Length]!;
        }
    }

    public void Add(TelemetryPacket<T> packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (_count < _slots.Length)
        {
            _slots[(_oldest + _count) % _slots.Length] = packet;
            _count++;
            return;
        }

        _slots[_oldest] = packet;
        _oldest = (_oldest + 1) % _slots.Length;
    }

    public TelemetryPacket<T>? LastOrDefault() => _count == 0 ? null : this[_count - 1];

    public IReadOnlyList<TelemetryPacket<T>> Snapshot()
    {
        var copy = new TelemetryPacket<T>[_count];
        for (var i = 0; i < _count; i++)
        {
            copy[i] = this[i];
        }

        return copy;
    }

    public IEnumerator<TelemetryPacket<T>> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
