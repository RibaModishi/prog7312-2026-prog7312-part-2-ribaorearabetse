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


// References (IIE Harvard). Accessed: 19 October 2026.
// Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to algorithms. 4th edn. Cambridge, MA: MIT Press.
// East, D. (n.d.) Getting started with Firebase Auth on the Web. Available at: https://www.youtube.com/watch?v=-OKrloDzGpU
// Firebase (n.d.) Firebase Authentication REST API. Available at: https://firebase.google.com/docs/reference/rest/auth
// Firebase (n.d.) Get started with Firebase Authentication on websites. Available at: https://firebase.google.com/docs/auth/web/start
// Firebase (n.d.) Verify ID tokens. Available at: https://firebase.google.com/docs/auth/admin/verify-id-tokens
// Google Cloud (n.d.) Method: accounts.signInWithPassword. Available at: https://docs.cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithPassword
// Janbodnar (n.d.) C# PriorityQueue. Available at: https://zetcode.com/csharp/priority-queue/
// Microsoft (n.d.) Dictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2
// Microsoft (n.d.) HashSet<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1
// Microsoft (n.d.) Overview of ASP.NET Core authentication. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
// Microsoft (n.d.) PriorityQueue<TElement,TPriority> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2
// Microsoft (n.d.) Queue<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1
// Microsoft (n.d.) SortedDictionary<TKey,TValue> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2
// Microsoft (n.d.) Stack<T> class. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1
// Microsoft (n.d.) Use HttpClientFactory to implement resilient HTTP requests. Available at: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory
// NetMentor (2021) PriorityQueue in C#. Available at: https://www.netmentor.es/entrada/en/priorityqueue-csharp
// .NET Core Tutorials (n.d.) PriorityQueue in C# .NET. Available at: https://dotnetcoretutorials.com/priorityqueue-in-net/
// Roy Chowdhury, K. (2024) Learn Advanced C# Part 1: Step by step build PriorityQueue. Available at: https://www.youtube.com/watch?v=ikbp6vph-6s
// YouTube (n.d.) C# Collections tutorial: List, HashSet, Dictionary, Queue and Stack. Available at: https://www.youtube.com/watch?v=4Mi4OuSMo4A
// YouTube (n.d.) C# collections: Stack, Queue and Dictionary. Available at: https://www.youtube.com/watch?v=GmftVtxrKUc
// YouTube (n.d.) C# hands on: queues. Available at: https://www.youtube.com/watch?v=huzE208-ha8
// YouTube (n.d.) Master the PriorityQueue, a collection in .NET. Available at: https://www.youtube.com/watch?v=d262kUQKT0E

