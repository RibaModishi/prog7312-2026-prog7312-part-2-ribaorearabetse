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


// References (IIE Harvard). Accessed: 19 October 2026.
// Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to algorithms. 4th edn. Cambridge, MA: MIT Press.
// East, D. (n.d.) Getting started with Firebase Auth on the Web. Available at: https://www.youtube.com/watch?v=-OKrloDzGpU
// Firebase (n.d.) Firebase Authentication REST API. Available at: https://firebase.google.com/docs/reference/rest/auth
// Firebase (n.d.) Get started with Firebase Authentication on websites. Available at: https://firebase.google.com/docs/auth/web/start
// Firebase (n.d.) Verify ID tokens. Available at: https://firebase.google.com/docs/auth/admin/verify-id-tokens
// Google Cloud (n.d.) Method: accounts.signInWithPassword. Available at: https://docs.cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithPassword
// Janbodnar (n.d.) C# PriorityQueue. Available at: https://zetcode.com/csharp/priority-queue/
// Microsoft (n.d.) Dictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2
// Microsoft (n.d.) HashSet<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1
// Microsoft (n.d.) Overview of ASP.NET Core authentication. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
// Microsoft (n.d.) PriorityQueue<TElement,TPriority> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2
// Microsoft (n.d.) Queue<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1
// Microsoft (n.d.) SortedDictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2
// Microsoft (n.d.) Stack<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1
// Microsoft (n.d.) Use HttpClientFactory to implement resilient HTTP requests. Available at: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory
// NetMentor (2021) PriorityQueue in C#. Available at: https://www.netmentor.es/entrada/en/priorityqueue-csharp
// .NET Core Tutorials (n.d.) PriorityQueue in C# .NET. Available at: https://dotnetcoretutorials.com/priorityqueue-in-net/
// Roy Chowdhury, K. (2024) Learn Advanced C# Part 1: Step by step build PriorityQueue. Available at: https://www.youtube.com/watch?v=ikbp6vph-6s
// YouTube (n.d.) C# Collections tutorial: List, HashSet, Dictionary, Queue and Stack. Available at: https://www.youtube.com/watch?v=4Mi4OuSMo4A
// YouTube (n.d.) C# collections: Stack, Queue and Dictionary. Available at: https://www.youtube.com/watch?v=GmftVtxrKUc
// YouTube (n.d.) C# hands on: queues. Available at: https://www.youtube.com/watch?v=huzE208-ha8
// YouTube (n.d.) Master the PriorityQueue, a collection in .NET. Available at: https://www.youtube.com/watch?v=d262kUQKT0E

