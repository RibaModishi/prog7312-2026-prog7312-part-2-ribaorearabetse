using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Suggests actions from recorded searches and commands. A suggestion is only emitted when the
/// same pump command has followed a low moisture reading at least twice.
/// </summary>
public sealed class PredictiveEngine
{
    public static readonly TimeSpan PatternWindow = TimeSpan.FromMinutes(5);
    public const double MoistureSuggestionThreshold = 20;
    public const int MinimumPatternCount = 2;

    private readonly object _gate = new();
    private readonly List<OperatorEvent> _events = [];
    private readonly Dictionary<string, int> _patternCounts = new(StringComparer.OrdinalIgnoreCase);

    public void RecordSearch(string query, DateTime utcNow, RealtimeStructures structures)
    {
        lock (_gate)
        {
            _events.Add(new OperatorEvent { Kind = "search", Text = query, TimestampUtc = utcNow });

            if (string.IsNullOrWhiteSpace(query)
                || structures.RecentLowMoisture(utcNow, PatternWindow, MoistureSuggestionThreshold).Count == 0)
            {
                return;
            }

            foreach (var device in structures.Devices())
            {
                if (device.Category != SensorCategory.Actuator)
                {
                    continue;
                }

                var name = Display(device);
                if (!name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                    && !query.Contains(name, StringComparison.OrdinalIgnoreCase)
                    && !device.MacAddress.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = "search-moisture|" + device.MacAddress;
                _patternCounts[key] = _patternCounts.TryGetValue(key, out var count) ? count + 1 : 1;
            }
        }
    }

    public void RecordCommand(string sensorId, string action, double? moistureNearby, DateTime utcNow)
    {
        lock (_gate)
        {
            _events.Add(new OperatorEvent
            {
                Kind = "command",
                Text = action,
                SensorId = sensorId,
                Action = action,
                MoistureNearby = moistureNearby,
                TimestampUtc = utcNow
            });

            if (action == "StartPump" && moistureNearby is < MoistureSuggestionThreshold)
            {
                var key = "moisture-low|" + sensorId;
                _patternCounts[key] = _patternCounts.TryGetValue(key, out var count) ? count + 1 : 1;
            }
        }
    }

    public IReadOnlyList<SuggestedAction> Suggest(RealtimeStructures structures)
    {
        var suggestions = new List<SuggestedAction>();

        foreach (var device in structures.Devices())
        {
            if (structures.IsDisconnected(device.MacAddress))
            {
                suggestions.Add(new SuggestedAction
                {
                    Title = "Device silent",
                    Detail = $"{Display(device)} has stopped publishing.",
                    SensorId = device.MacAddress
                });
            }

            var last = structures.LastValue(device.MacAddress);
            if (device.Category == SensorCategory.Environmental && last is < MoistureSuggestionThreshold)
            {
                suggestions.Add(new SuggestedAction
                {
                    Title = "Moisture out of range",
                    Detail = $"{Display(device)} is at {last:0.#}, below {MoistureSuggestionThreshold}.",
                    SensorId = device.MacAddress
                });
            }

            if (device.Category == SensorCategory.PowerConsumption && last >= RealtimeStructures.CriticalPowerWatts)
            {
                suggestions.Add(new SuggestedAction
                {
                    Title = "Power spike",
                    Detail = $"{Display(device)} is drawing {last:0.#}W.",
                    SensorId = device.MacAddress
                });
            }
        }

        lock (_gate)
        {
            foreach (var (key, count) in _patternCounts)
            {
                var fromSearch = key.StartsWith("search-moisture|", StringComparison.Ordinal);
                var fromCommand = key.StartsWith("moisture-low|", StringComparison.Ordinal);
                if (count < MinimumPatternCount || (!fromSearch && !fromCommand))
                {
                    continue;
                }

                var pumpId = key[(key.IndexOf('|') + 1)..];
                var stillLow = structures.RecentLowMoisture(DateTime.UtcNow, PatternWindow, MoistureSuggestionThreshold);
                if (stillLow.Count == 0)
                {
                    continue;
                }

                var pump = structures.Devices().FirstOrDefault(device =>
                    device.MacAddress.Equals(pumpId, StringComparison.OrdinalIgnoreCase));
                var pumpName = pump is null ? pumpId : Display(pump);
                var moistureName = Display(structures.Devices().First(device =>
                    device.MacAddress.Equals(stillLow[0].SensorId, StringComparison.OrdinalIgnoreCase)));
                var reason = fromSearch
                    ? $"You searched for {pumpName} {count} times while {moistureName} was below {MoistureSuggestionThreshold}."
                    : $"You started {pumpName} {count} times after {moistureName} dropped below {MoistureSuggestionThreshold}.";

                suggestions.Insert(0, new SuggestedAction
                {
                    Title = "Suggested action",
                    Detail = $"Start {pumpName}. {reason}",
                    SensorId = pumpId,
                    Action = "StartPump"
                });
            }
        }

        return suggestions;
    }

    private static string Display(Contracts.SensorRecord device) =>
        string.IsNullOrWhiteSpace(device.DisplayName) ? device.MacAddress : device.DisplayName;

    private sealed class OperatorEvent
    {
        public required string Kind { get; init; }

        public required string Text { get; init; }

        public string? SensorId { get; init; }

        public string? Action { get; init; }

        public double? MoistureNearby { get; init; }

        public DateTime TimestampUtc { get; init; }
    }
}
