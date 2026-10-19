using SmartX.Shared.Contracts;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Dedicated holder for the Part 2 collections. Each member is named for the requirement it serves.
/// </summary>
public sealed class RealtimeStructures : SmartX.Shared.Realtime.RealtimeStructures
{
    public new Queue<TelemetryEnvelope> TelemetryQueue => base.TelemetryQueue;

    public new PriorityQueue<TelemetryEnvelope, int> CriticalAlertQueue => base.CriticalAlertQueue;

    public new Stack<DeviceCommand> CommandUndoStack => base.CommandUndoStack;

    public new Dictionary<string, SensorRecord> DeviceRegistry => base.DeviceRegistry;

    public new Dictionary<string, SortedDictionary<DateTime, TelemetryPoint>> SensorTimeline => base.SensorTimeline;

    public new HashSet<string> DisconnectedNodes => base.DisconnectedNodes;
}
