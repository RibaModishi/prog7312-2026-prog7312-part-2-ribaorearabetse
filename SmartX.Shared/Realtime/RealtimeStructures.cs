using SmartX.Shared.Contracts;
using SmartX.Shared.Telemetry;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Part 2 collections, named the way the brief describes them so each requirement is easy to find.
/// </summary>
public class RealtimeStructures
{
    public const double CriticalMoisture = 15;
    public const double CriticalPowerWatts = 2_000;

    private readonly object _gate = new();
    private readonly List<string> _processedOrder = [];

    public Queue<TelemetryEnvelope> TelemetryQueue { get; } = new();

    public PriorityQueue<TelemetryEnvelope, int> CriticalAlertQueue { get; } = new();

    public Stack<DeviceCommand> CommandUndoStack { get; } = new();

    public Dictionary<string, SensorRecord> DeviceRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, SortedDictionary<DateTime, TelemetryPoint>> SensorTimeline { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> DisconnectedNodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, bool> ActuatorState { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void IndexDevice(SensorRecord sensor)
    {
        lock (_gate)
        {
            DeviceRegistry[sensor.MacAddress] = sensor;
        }
    }

    public IReadOnlyList<SensorRecord> Devices()
    {
        lock (_gate)
        {
            return DeviceRegistry.Values.ToList();
        }
    }

    public bool TryGetDevice(string sensorId, out SensorRecord sensor)
    {
        lock (_gate)
        {
            return DeviceRegistry.TryGetValue(sensorId, out sensor!);
        }
    }

    public static bool IsCritical(SensorCategory category, double value) =>
        category switch
        {
            SensorCategory.Environmental => value <= CriticalMoisture,
            SensorCategory.PowerConsumption => value >= CriticalPowerWatts,
            _ => false
        };

    /// <summary>
    /// Critical readings skip <see cref="TelemetryQueue"/> and land on <see cref="CriticalAlertQueue"/>
    /// with priority 0 so they are drained first.
    /// </summary>
    public void EnqueueReading(SensorRecord sensor, double value)
    {
        var envelope = new TelemetryEnvelope
        {
            SensorId = sensor.MacAddress,
            Category = sensor.Category,
            Value = value
        };

        lock (_gate)
        {
            if (IsCritical(sensor.Category, value))
            {
                CriticalAlertQueue.Enqueue(envelope, 0);
                return;
            }

            TelemetryQueue.Enqueue(envelope);
        }
    }

    public bool TryDrainNext(out TelemetryEnvelope? envelope, out bool wasCritical)
    {
        lock (_gate)
        {
            if (CriticalAlertQueue.TryDequeue(out var critical, out _))
            {
                envelope = critical;
                wasCritical = true;
                _processedOrder.Add("critical:" + critical.SensorId);
                return true;
            }

            if (TelemetryQueue.TryDequeue(out var normal))
            {
                envelope = normal;
                wasCritical = false;
                _processedOrder.Add("normal:" + normal.SensorId);
                return true;
            }
        }

        envelope = null;
        wasCritical = false;
        return false;
    }

    public void AppendTimeline(TelemetryEnvelope envelope)
    {
        lock (_gate)
        {
            if (!SensorTimeline.TryGetValue(envelope.SensorId, out var series))
            {
                series = new SortedDictionary<DateTime, TelemetryPoint>();
                SensorTimeline[envelope.SensorId] = series;
            }

            var stamp = envelope.EnqueuedAtUtc;
            while (series.ContainsKey(stamp))
            {
                stamp = stamp.AddTicks(1);
            }

            series[stamp] = new TelemetryPoint
            {
                SensorId = envelope.SensorId,
                Value = envelope.Value,
                TimestampUtc = stamp
            };
        }
    }

    public IReadOnlyList<double> Sparkline(string sensorId, int take = 12)
    {
        lock (_gate)
        {
            if (!SensorTimeline.TryGetValue(sensorId, out var series))
            {
                return [];
            }

            return series.Values.TakeLast(take).Select(point => point.Value).ToList();
        }
    }

    public IReadOnlyList<(string SensorId, double Value, DateTime TimestampUtc)> RecentLowMoisture(
        DateTime utcNow,
        TimeSpan window,
        double threshold)
    {
        lock (_gate)
        {
            var hits = new List<(string, double, DateTime)>();
            foreach (var (sensorId, series) in SensorTimeline)
            {
                if (series.Count == 0
                    || !DeviceRegistry.TryGetValue(sensorId, out var device)
                    || device.Category != SensorCategory.Environmental)
                {
                    continue;
                }

                var last = series.Values.Last();
                if (last.Value < threshold && utcNow - last.TimestampUtc <= window)
                {
                    hits.Add((sensorId, last.Value, last.TimestampUtc));
                }
            }

            return hits;
        }
    }

    public double? LastValue(string sensorId)
    {
        lock (_gate)
        {
            if (!SensorTimeline.TryGetValue(sensorId, out var series) || series.Count == 0)
            {
                return null;
            }

            return series.Values.Last().Value;
        }
    }

    public void PushCommand(DeviceCommand command)
    {
        lock (_gate)
        {
            CommandUndoStack.Push(command);
            ApplyAction(command.SensorId, command.Action);
        }
    }

    /// <summary>
    /// Pops the last override and applies its inverse. An empty stack returns false and does not throw.
    /// </summary>
    public bool TryUndo(out DeviceCommand? undone, out string message)
    {
        lock (_gate)
        {
            if (CommandUndoStack.Count == 0)
            {
                undone = null;
                message = "Nothing to undo.";
                return false;
            }

            undone = CommandUndoStack.Pop();
            ApplyAction(undone.SensorId, undone.InverseAction);
            message = $"Undid {undone.Action} on {undone.SensorId}.";
            return true;
        }
    }

    public bool MarkDisconnected(string sensorId)
    {
        lock (_gate)
        {
            return DisconnectedNodes.Add(sensorId);
        }
    }

    public void ClearDisconnected(string sensorId)
    {
        lock (_gate)
        {
            DisconnectedNodes.Remove(sensorId);
        }
    }

    public bool? ActuatorOn(string sensorId)
    {
        lock (_gate)
        {
            return ActuatorState.TryGetValue(sensorId, out var on) ? on : null;
        }
    }

    public bool IsDisconnected(string sensorId)
    {
        lock (_gate)
        {
            return DisconnectedNodes.Contains(sensorId);
        }
    }

    public IReadOnlyList<string> ProcessedOrder()
    {
        lock (_gate)
        {
            return _processedOrder.ToList();
        }
    }

    public (int Normal, int Critical, int UndoDepth) Depths()
    {
        lock (_gate)
        {
            return (TelemetryQueue.Count, CriticalAlertQueue.Count, CommandUndoStack.Count);
        }
    }

    private void ApplyAction(string sensorId, string action)
    {
        ActuatorState[sensorId] = action switch
        {
            "StartPump" or "RelayOn" => true,
            "StopPump" or "RelayOff" => false,
            "ToggleRelay" => !ActuatorState.TryGetValue(sensorId, out var on) || !on,
            _ => ActuatorState.TryGetValue(sensorId, out var current) && current
        };
    }
}
