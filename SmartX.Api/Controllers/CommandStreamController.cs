using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Services;
using SmartX.Shared.Contracts;
using Structures = SmartX.Api.Services.RealtimeStructures;
using SmartX.Shared.Realtime;
using SmartX.Shared.Telemetry;

namespace SmartX.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/command-stream")]
public class CommandStreamController(
    Structures realtime,
    PredictiveEngine predictive,
    AlertAggregationService alerts,
    SmartXStore store,
    TelemetryQueueProcessor processor) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get([FromQuery] string? category, [FromQuery] string? alertState, [FromQuery] string? q)
    {
        var devices = BuildDevices()
            .Where(device => Matches(device, category, alertState, q))
            .ToList();

        return Ok(new
        {
            devices,
            suggestions = predictive.Suggest(realtime),
            depths = realtime.Depths(),
            processedOrder = realtime.ProcessedOrder()
        });
    }

    [HttpPost("search")]
    public ActionResult<object> Search([FromBody] SearchRequest request)
    {
        predictive.RecordSearch(request.Query ?? string.Empty, DateTime.UtcNow, realtime);
        var devices = BuildDevices()
            .Where(device => Matches(device, request.Category, request.AlertState, request.Query))
            .ToList();

        return Ok(new { devices, suggestions = predictive.Suggest(realtime) });
    }

    [HttpPost("priority-demo")]
    public ActionResult<object> PriorityDemo()
    {
        var moisture = Ensure("AA:BB:CC:DD:EE:01", SensorCategory.Environmental, "Moisture Sensor B");
        var power = Ensure("AA:BB:CC:DD:EE:02", SensorCategory.PowerConsumption, "Power Rail A");

        using (processor.PauseBackgroundDrain())
        {
            Thread.Sleep(40);
            realtime.EnqueueReading(moisture, 40);
            realtime.EnqueueReading(power, 2100);
            processor.DrainPending();
        }

        return Ok(new
        {
            processedOrder = realtime.ProcessedOrder(),
            note = "The 2100W packet is labelled critical and is processed before the moisture packet that was queued first."
        });
    }

    private SensorRecord Ensure(string mac, SensorCategory category, string name)
    {
        if (store.TryGetSensor(mac, out var existing))
        {
            realtime.IndexDevice(existing);
            return existing;
        }

        var result = store.RegisterSensor(new SensorRegistrationRequest
        {
            MacAddress = mac,
            DisplayName = name,
            Category = category,
            FacilityName = "Greenhouse",
            ZoneName = "Zone 1"
        });

        realtime.IndexDevice(result.Sensor!);
        return result.Sensor!;
    }

    [HttpGet("trace")]
    public ActionResult<object> Trace()
    {
        processor.DrainPending();
        var depths = realtime.Depths();
        return Ok(new
        {
            normalQueue = depths.Normal,
            criticalQueue = depths.Critical,
            undoDepth = depths.UndoDepth,
            processedOrder = realtime.ProcessedOrder()
        });
    }

    private IEnumerable<CommandStreamDevice> BuildDevices()
    {
        var alerted = alerts.GetActiveGroups()
            .Select(group => group.SensorId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sensor in realtime.Devices())
        {
            var disconnected = realtime.IsDisconnected(sensor.MacAddress);
            var alertState = disconnected
                ? "disconnected"
                : alerted.Contains(sensor.MacAddress) ? "threshold" : "healthy";

            yield return new CommandStreamDevice
            {
                SensorId = sensor.MacAddress,
                DisplayName = string.IsNullOrWhiteSpace(sensor.DisplayName) ? sensor.MacAddress : sensor.DisplayName,
                Category = sensor.Category,
                DeploymentPath = sensor.DeploymentPath,
                LastValue = realtime.LastValue(sensor.MacAddress),
                AlertState = alertState,
                ActuatorOn = sensor.Category == SensorCategory.Actuator ? realtime.ActuatorOn(sensor.MacAddress) : null,
                Sparkline = realtime.Sparkline(sensor.MacAddress).ToList()
            };
        }
    }

    private static bool Matches(CommandStreamDevice device, string? category, string? alertState, string? query)
    {
        if (!string.IsNullOrWhiteSpace(category)
            && !category.Equals("all", StringComparison.OrdinalIgnoreCase)
            && !device.Category.ToString().Equals(category, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(alertState)
            && !alertState.Equals("all", StringComparison.OrdinalIgnoreCase)
            && !device.AlertState.Equals(alertState, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return device.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || device.SensorId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || device.DeploymentPath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SearchRequest
{
    public string? Query { get; set; }

    public string? Category { get; set; }

    public string? AlertState { get; set; }
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

