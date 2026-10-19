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

    private static SensorRecord Sensor(string mac, SensorCategory category, string name = "") => new()
    {
        MacAddress = mac,
        DisplayName = name,
        Category = category,
        DeploymentPath = "Smart-X/Lab/Zone 1"
    };
}
