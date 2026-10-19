namespace SmartX.Shared.Realtime;

/// <summary>
/// A manual override pushed onto the undo stack. <see cref="InverseAction"/> is what Undo applies.
/// </summary>
public sealed class DeviceCommand
{
    public required string SensorId { get; init; }

    public required string Action { get; init; }

    public required string InverseAction { get; init; }

    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;
}
