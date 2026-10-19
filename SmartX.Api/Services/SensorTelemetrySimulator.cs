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
