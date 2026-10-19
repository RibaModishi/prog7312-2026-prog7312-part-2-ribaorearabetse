namespace SmartX.Shared.Realtime;

public sealed class SuggestedAction
{
    public required string Title { get; init; }

    public required string Detail { get; init; }

    public string? SensorId { get; init; }

    public string? Action { get; init; }
}
