namespace SmartX.Shared.Realtime;

/// <summary>
/// One point on a device timeline. Stored in a <see cref="System.Collections.Generic.SortedDictionary{TKey,TValue}"/>
/// keyed by timestamp so the chart is already in order.
/// </summary>
public sealed class TelemetryPoint
{
    public required string SensorId { get; init; }

    public required double Value { get; init; }

    public DateTime TimestampUtc { get; init; }
}
