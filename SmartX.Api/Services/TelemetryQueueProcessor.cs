using SmartX.Shared.Telemetry;

namespace SmartX.Api.Services;

/// <summary>
/// Drains <see cref="RealtimeStructures.CriticalAlertQueue"/> before
/// <see cref="RealtimeStructures.TelemetryQueue"/>, then writes the packet into the store and the
/// per-device sorted timeline.
/// </summary>
public class TelemetryQueueProcessor(
    RealtimeStructures realtime,
    SmartXStore store,
    AlertAggregationService alerts,
    ILogger<TelemetryQueueProcessor> logger) : BackgroundService
{
    private int _paused;

    public IDisposable PauseBackgroundDrain()
    {
        Interlocked.Increment(ref _paused);
        return new PauseScope(this);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (Volatile.Read(ref _paused) > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), stoppingToken);
                continue;
            }

            var drained = 0;
            while (realtime.TryDrainNext(out var envelope, out var wasCritical) && envelope is not null)
            {
                Apply(envelope, wasCritical);
                drained++;
            }

            if (drained == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
            }
        }
    }

    public int DrainPending()
    {
        var drained = 0;
        while (realtime.TryDrainNext(out var envelope, out var wasCritical) && envelope is not null)
        {
            Apply(envelope, wasCritical);
            drained++;
        }

        return drained;
    }

    private void Apply(SmartX.Shared.Realtime.TelemetryEnvelope envelope, bool wasCritical)
    {
        switch (envelope.Category)
        {
            case SensorCategory.Environmental:
                store.RecordEnvironmental(envelope.SensorId, (float)envelope.Value);
                break;
            case SensorCategory.PowerConsumption:
                store.RecordPower(envelope.SensorId, (int)envelope.Value);
                break;
            case SensorCategory.Actuator:
                store.RecordActuator(envelope.SensorId, envelope.Value >= 0.5);
                break;
        }

        realtime.AppendTimeline(envelope);
        realtime.ClearDisconnected(envelope.SensorId);

        if (!realtime.TryGetDevice(envelope.SensorId, out var sensor) &&
            !store.TryGetSensor(envelope.SensorId, out sensor))
        {
            return;
        }

        alerts.CheckThreshold(sensor.MacAddress, sensor.DeploymentPath, sensor.Category, envelope.Value);

        if (wasCritical)
        {
            logger.LogInformation("Processed critical reading {Value} for {SensorId} ahead of the normal queue.", envelope.Value, envelope.SensorId);
        }
    }

    private sealed class PauseScope(TelemetryQueueProcessor processor) : IDisposable
    {
        public void Dispose() => Interlocked.Decrement(ref processor._paused);
    }
}
