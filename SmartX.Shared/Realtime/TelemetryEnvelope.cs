using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Realtime;

/// <summary>
/// One telemetry packet waiting to be processed. The queue holds these in arrival order.
/// </summary>
public sealed class TelemetryEnvelope
{
    public required string SensorId { get; init; }

    public required SensorCategory Category { get; init; }

    public required double Value { get; init; }

    public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;
}
