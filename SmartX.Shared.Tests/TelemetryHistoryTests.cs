using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Tests;

public class TelemetryHistoryTests
{
    [Fact]
    public void Add_KeepsPacketsInInsertionOrder()
    {
        var history = new TelemetryHistory<int>(capacity: 4);
        history.Add(Packet(1));
        history.Add(Packet(2));
        history.Add(Packet(3));

        Assert.Equal(3, history.Count);
        Assert.Equal([1, 2, 3], history.Select(packet => packet.Value));
        Assert.Equal(3, history.LastOrDefault()!.Value);
    }

    [Fact]
    public void Add_OverwritesTheOldestPacketOnceCapacityIsReached()
    {
        var history = new TelemetryHistory<int>(capacity: 3);
        history.Add(Packet(1));
        history.Add(Packet(2));
        history.Add(Packet(3));
        history.Add(Packet(4));
        history.Add(Packet(5));

        Assert.Equal(3, history.Count);
        Assert.Equal([3, 4, 5], history.Select(packet => packet.Value));
        Assert.Equal(5, history.LastOrDefault()!.Value);
    }

    [Fact]
    public void Snapshot_IsACopyThatDoesNotTrackLaterAdds()
    {
        var history = new TelemetryHistory<int>(capacity: 4);
        history.Add(Packet(1));

        var snapshot = history.Snapshot();
        history.Add(Packet(2));

        Assert.Single(snapshot);
        Assert.Equal(1, snapshot[0].Value);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Indexer_ThrowsWhenTheIndexIsOutsideTheRing()
    {
        var history = new TelemetryHistory<int>(capacity: 2);
        history.Add(Packet(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => history[1]);
    }

    private static TelemetryPacket<int> Packet(int watts) =>
        new("AA:BB:CC:DD:EE:01", SensorCategory.PowerConsumption, watts);
}
