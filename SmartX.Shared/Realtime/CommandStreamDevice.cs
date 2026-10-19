using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Realtime;

public sealed class CommandStreamDevice
{
    public required string SensorId { get; init; }

    public required string DisplayName { get; init; }

    public required SensorCategory Category { get; init; }

    public required string DeploymentPath { get; init; }

    public double? LastValue { get; init; }

    public required string AlertState { get; init; }

    public bool? ActuatorOn { get; init; }

    public List<double> Sparkline { get; set; } = [];
}
