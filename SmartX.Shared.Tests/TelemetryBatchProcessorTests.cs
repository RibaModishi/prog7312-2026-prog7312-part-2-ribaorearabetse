using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Tests;

public class TelemetryBatchProcessorTests
{
    private static readonly DateTime BatchStart = new(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private static TelemetryBatchProcessor<int> CreateProcessor() =>
        new("ESP32-MTR-07", SensorCategory.PowerConsumption, PollingInterval);

    [Fact]
    public void ToJaggedArray_PreservesCyclesOfDifferentLengths()
    {
        var processor = CreateProcessor();
        processor.AddBatch([120, 135, 128]);
        processor.AddBatch([140, 152, 149, 151, 147]);

        var jagged = processor.ToJaggedArray();

        Assert.Equal(2, jagged.Length);
        Assert.Equal(3, jagged[0].Length);
        Assert.Equal(5, jagged[1].Length);
        Assert.Equal([120, 135, 128], jagged[0]);
        Assert.Equal([140, 152, 149, 151, 147], jagged[1]);
    }

    [Fact]
    public void Flatten_ReturnsOnePacketPerReadingRatherThanPerBatch()
    {
        var processor = CreateProcessor();
        processor.AddBatch([120, 135, 128]);
        processor.AddBatch([140, 152, 149, 151, 147]);

        var packets = processor.Flatten(BatchStart);

        Assert.Equal(8, packets.Count);
        Assert.Equal([120, 135, 128, 140, 152, 149, 151, 147], packets.Select(packet => packet.Value));
    }

    [Fact]
    public void Flatten_StampsEveryPacketWithTheOwningSensorAndCategory()
    {
        var processor = CreateProcessor();
        processor.AddBatch([120, 135]);
        processor.AddBatch([140]);

        var packets = processor.Flatten(BatchStart);

        Assert.All(packets, packet =>
        {
            Assert.Equal("ESP32-MTR-07", packet.SensorId);
            Assert.Equal(SensorCategory.PowerConsumption, packet.Category);
        });
    }

    [Fact]
    public void Flatten_SharesOneTimestampWithinABatchAndOffsetsConsecutiveBatches()
    {
        var processor = CreateProcessor();
        processor.AddBatch([120, 135, 128]);
        processor.AddBatch([140, 152]);

        var packets = processor.Flatten(BatchStart);

        Assert.All(packets.Take(3), packet => Assert.Equal(BatchStart, packet.Timestamp));
        Assert.All(packets.Skip(3), packet => Assert.Equal(BatchStart + PollingInterval, packet.Timestamp));
    }

    [Fact]
    public void Flatten_ReturnsAnEmptyListWhenNoBatchesHaveBeenAdded()
    {
        var processor = CreateProcessor();

        var packets = processor.Flatten(BatchStart);

        Assert.Empty(packets);
    }

    [Fact]
    public void Flatten_AcceptsADroppedCycleAndKeepsLaterReadingsOnTheTimeAxis()
    {
        var processor = CreateProcessor();
        processor.AddBatch([120]);
        processor.AddBatch([]);
        processor.AddBatch([140]);

        var packets = processor.Flatten(BatchStart);

        Assert.Equal(2, packets.Count);
        Assert.Equal(BatchStart, packets[0].Timestamp);
        Assert.Equal(BatchStart + (PollingInterval * 2), packets[1].Timestamp);
    }

    [Fact]
    public void AddBatch_AcceptsAnEmptyPollingCycleAsValidData()
    {
        var processor = CreateProcessor();

        processor.AddBatch([]);

        var jagged = processor.ToJaggedArray();
        Assert.Single(jagged);
        Assert.Empty(jagged[0]);
    }

    [Fact]
    public void AddBatch_ThrowsWhenTheCycleIsNull()
    {
        var processor = CreateProcessor();

        Assert.Throws<ArgumentNullException>(() => processor.AddBatch(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsABatchWithNoRegisteredSensor(string? sensorId)
    {
        Assert.Throws<ArgumentException>(() =>
            new TelemetryBatchProcessor<int>(sensorId!, SensorCategory.PowerConsumption, PollingInterval));
    }
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

