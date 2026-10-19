using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Tests;

public class TelemetryPacketTests
{
    [Fact]
    public void Wrapper_CarriesAFloatSoilMoistureReading()
    {
        var packet = new TelemetryPacket<float>("ESP32-SOIL-01", SensorCategory.Environmental, 42.8f);

        Assert.Equal("ESP32-SOIL-01", packet.SensorId);
        Assert.Equal(SensorCategory.Environmental, packet.Category);
        Assert.Equal(42.8f, packet.Value);
    }

    [Fact]
    public void Wrapper_CarriesAnIntegerWattageReading()
    {
        var packet = new TelemetryPacket<int>("ESP32-MTR-07", SensorCategory.PowerConsumption, 1_450);

        Assert.Equal(1_450, packet.Value);
    }

    [Fact]
    public void Wrapper_CarriesABooleanValveState()
    {
        var packet = new TelemetryPacket<bool>("ESP32-VLV-03", SensorCategory.Actuator, true);

        Assert.True(packet.Value);
    }

    [Fact]
    public void Wrapper_AcceptsACustomStructAsItsPayload()
    {
        var meter1 = new TelemetryPacket<PowerReading>(
            "ESP32-MTR-01", SensorCategory.PowerConsumption, new PowerReading(240.5));
        var meter2 = new TelemetryPacket<PowerReading>(
            "ESP32-MTR-02", SensorCategory.PowerConsumption, new PowerReading(112.25));

        var aggregate = meter1.Value + meter2.Value;

        Assert.Equal(352.75, aggregate.Watts, 3);
    }

    [Fact]
    public void Timestamp_DefaultsToCaptureTimeWhenNotSupplied()
    {
        var before = DateTime.UtcNow;

        var packet = new TelemetryPacket<int>("ESP32-MTR-07", SensorCategory.PowerConsumption, 900);

        Assert.InRange(packet.Timestamp, before, DateTime.UtcNow);
    }

    [Fact]
    public void Timestamp_IsPreservedWhenReplayingHistoricalTelemetry()
    {
        var captured = new DateTime(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);

        var packet = new TelemetryPacket<float>(
            "ESP32-SOIL-01", SensorCategory.Environmental, 31.2f, captured);

        Assert.Equal(captured, packet.Timestamp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAPacketWithNoRegisteredSensor(string? sensorId)
    {
        Assert.Throws<ArgumentException>(() =>
            new TelemetryPacket<float>(sensorId!, SensorCategory.Environmental, 12.5f));
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

