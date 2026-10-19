using System.Net.Http.Json;
using SmartX.Shared.Contracts;
using SmartX.Shared.Telemetry;

namespace SmartX.Api.Services;

/// <summary>
/// Publishes mock readings by POSTing to the same telemetry endpoints a physical device would call.
/// A dropped cycle is left unpublished so the disconnect watcher can notice it.
/// </summary>
public class SensorTelemetrySimulator(
    IHttpClientFactory httpClientFactory,
    SmartXStore store,
    ILogger<SensorTelemetrySimulator> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = SmartXStore.PollingInterval;

    private const double DroppedPollChance = 0.08;
    private const double ThresholdBreachChance = 0.10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        var published = 0;
        var skipped = 0;
        var client = httpClientFactory.CreateClient("device-ingest");

        foreach (var sensor in store.Sensors)
        {
            if (Random.Shared.NextDouble() < DroppedPollChance)
            {
                skipped++;
                continue;
            }

            try
            {
                if (await PublishAsync(client, sensor, stoppingToken))
                {
                    published++;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "Simulator could not reach the telemetry API for {SensorId}.", sensor.MacAddress);
            }
        }

        if (published + skipped > 0)
        {
            logger.LogInformation("Simulator cycle posted {Published} reading(s) to the API and skipped {Skipped}.", published, skipped);
        }
    }

    private static async Task<bool> PublishAsync(HttpClient client, SensorRecord sensor, CancellationToken stoppingToken)
    {
        var (path, body) = sensor.Category switch
        {
            SensorCategory.Environmental => ("api/telemetry/environmental", (object)new { sensorId = sensor.MacAddress, value = NextEnvironmental() }),
            SensorCategory.PowerConsumption => ("api/telemetry/power", new { sensorId = sensor.MacAddress, value = NextPower() }),
            SensorCategory.Actuator => ("api/telemetry/actuator", new { sensorId = sensor.MacAddress, value = Random.Shared.Next(2) == 1 }),
            _ => (null, null)
        };

        if (path is null || body is null)
        {
            return false;
        }

        using var response = await client.PostAsJsonAsync(path, body, stoppingToken);
        return response.IsSuccessStatusCode;
    }

    private static float NextEnvironmental()
    {
        if (Random.Shared.NextDouble() < ThresholdBreachChance)
        {
            return Random.Shared.Next(2) == 0
                ? Random.Shared.NextSingle() * 9f
                : 90.5f + Random.Shared.NextSingle() * 9.5f;
        }

        return 30f + Random.Shared.NextSingle() * 40f;
    }

    private static int NextPower()
    {
        if (Random.Shared.NextDouble() < ThresholdBreachChance)
        {
            return Random.Shared.Next(1_501, 2_501);
        }

        return Random.Shared.Next(200, 1_201);
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

