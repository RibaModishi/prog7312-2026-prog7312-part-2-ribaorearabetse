using SmartX.Shared.Contracts;
using SmartX.Shared.Realtime;
using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Tests;

public class RealtimeStructureTests
{
    [Fact]
    public void Normal_queue_drains_first_in_first_out()
    {
        var structures = new RealtimeStructures();
        structures.EnqueueReading(Sensor("AA:BB:CC:DD:EE:01", SensorCategory.Environmental), 40);
        structures.EnqueueReading(Sensor("AA:BB:CC:DD:EE:02", SensorCategory.Environmental), 41);

        Assert.True(structures.TryDrainNext(out var first, out var firstCritical));
        Assert.True(structures.TryDrainNext(out var second, out var secondCritical));

        Assert.False(firstCritical);
        Assert.False(secondCritical);
        Assert.Equal("AA:BB:CC:DD:EE:01", first!.SensorId);
        Assert.Equal("AA:BB:CC:DD:EE:02", second!.SensorId);
    }

    [Fact]
    public void Critical_reading_bypasses_packets_already_waiting()
    {
        var structures = new RealtimeStructures();
        structures.EnqueueReading(Sensor("AA:BB:CC:DD:EE:01", SensorCategory.Environmental), 40);
        structures.EnqueueReading(Sensor("AA:BB:CC:DD:EE:02", SensorCategory.Environmental), 10);

        Assert.True(structures.TryDrainNext(out var first, out var wasCritical));
        Assert.True(wasCritical);
        Assert.Equal("AA:BB:CC:DD:EE:02", first!.SensorId);
        Assert.Equal("critical:AA:BB:CC:DD:EE:02", structures.ProcessedOrder()[0]);
    }

    [Fact]
    public void Undo_on_an_empty_stack_does_not_throw()
    {
        var structures = new RealtimeStructures();

        var undone = structures.TryUndo(out var command, out var message);

        Assert.False(undone);
        Assert.Null(command);
        Assert.Equal("Nothing to undo.", message);
    }

    [Fact]
    public void Disconnect_set_ignores_a_duplicate_node()
    {
        var structures = new RealtimeStructures();

        Assert.True(structures.MarkDisconnected("AA:BB:CC:DD:EE:01"));
        Assert.False(structures.MarkDisconnected("aa:bb:cc:dd:ee:01"));
        Assert.Single(structures.DisconnectedNodes);
    }

    [Fact]
    public void Suggestion_appears_after_the_same_pump_follows_low_moisture_twice()
    {
        var structures = new RealtimeStructures();
        var moisture = Sensor("AA:BB:CC:DD:EE:10", SensorCategory.Environmental, "Moisture Sensor B");
        var pump = Sensor("AA:BB:CC:DD:EE:20", SensorCategory.Actuator, "Pump A");
        structures.IndexDevice(moisture);
        structures.IndexDevice(pump);
        structures.AppendTimeline(new TelemetryEnvelope
        {
            SensorId = moisture.MacAddress,
            Category = SensorCategory.Environmental,
            Value = 12
        });

        var engine = new PredictiveEngine();
        engine.RecordCommand(pump.MacAddress, "StartPump", 12, DateTime.UtcNow);
        Assert.DoesNotContain(engine.Suggest(structures), item => item.Action == "StartPump");

        engine.RecordCommand(pump.MacAddress, "StartPump", 11, DateTime.UtcNow);
        var suggestion = Assert.Single(engine.Suggest(structures), item => item.Action == "StartPump");
        Assert.Contains("Pump", suggestion.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Searching_for_a_pump_while_moisture_is_low_becomes_a_suggestion()
    {
        var structures = new RealtimeStructures();
        var moisture = Sensor("AA:BB:CC:DD:EE:10", SensorCategory.Environmental, "Moisture Sensor B");
        var pump = Sensor("AA:BB:CC:DD:EE:20", SensorCategory.Actuator, "Pump A");
        structures.IndexDevice(moisture);
        structures.IndexDevice(pump);
        structures.AppendTimeline(new TelemetryEnvelope
        {
            SensorId = moisture.MacAddress,
            Category = SensorCategory.Environmental,
            Value = 12
        });

        var engine = new PredictiveEngine();
        var now = DateTime.UtcNow;
        engine.RecordSearch("Pump A", now, structures);
        Assert.DoesNotContain(engine.Suggest(structures), item => item.Action == "StartPump");

        engine.RecordSearch("Pump A", now, structures);
        var suggestion = Assert.Single(engine.Suggest(structures), item => item.Action == "StartPump");
        Assert.Contains("searched", suggestion.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static SensorRecord Sensor(string mac, SensorCategory category, string name = "") => new()
    {
        MacAddress = mac,
        DisplayName = name,
        Category = category,
        DeploymentPath = "Smart-X/Lab/Zone 1"
    };
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

