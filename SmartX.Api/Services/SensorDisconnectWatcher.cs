using SmartX.Shared.Contracts;

namespace SmartX.Api.Services;

/// <summary>
/// Watches for sensors that have gone quiet. A delayed ESP32 poll is expected; silence that lasts
/// several polling intervals is not, and is raised as a disconnect rather than guessed at from a
/// stale reading. Each overdue sensor is recorded once per episode — the next sweep must not mint
/// a fresh alert just because the node is still silent — and the episode ends the moment a
/// reading arrives again.
/// </summary>
public class SensorDisconnectWatcher(SmartXStore store, AlertAggregationService alerts, RealtimeStructures realtime) : BackgroundService
{
    /// <summary>
    /// Tunable. Frequent enough to notice a drop within a couple of poll cycles, sparse enough
    /// that the sweep itself is not a load on the gateway.
    /// </summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Tunable. Four times the nominal 5-second poll, so one or two dropped packets are tolerated
    /// and a node that has actually left the mesh is not.
    /// </summary>
    public static readonly TimeSpan OverdueAfter = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            Sweep(DateTime.UtcNow);
        }
    }

    internal void Sweep(DateTime utcNow)
    {
        foreach (var (sensor, lastSeenUtc) in store.SnapshotLastSeen())
        {
            var overdue = utcNow - lastSeenUtc > OverdueAfter;

            if (!overdue)
            {
                // A fresh reading ends the episode. The next silence is a new disconnect, not a
                // continuation of this one, which is why the sensor is removed from the set
                // rather than left marked.
                realtime.ClearDisconnected(sensor.MacAddress);
                continue;
            }

            if (!realtime.MarkDisconnected(sensor.MacAddress))
            {
                continue;
            }

            var silentFor = utcNow - lastSeenUtc;
            alerts.RecordAlert(new Alert
            {
                SensorId = sensor.MacAddress,
                DeploymentPath = sensor.DeploymentPath,
                Category = sensor.Category,
                Type = AlertType.Disconnect,
                Message = $"Sensor '{sensor.MacAddress}' has not published for {silentFor.TotalSeconds:0}s (last seen {lastSeenUtc:HH:mm:ss} UTC).",
                Timestamp = utcNow
            });
        }
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

