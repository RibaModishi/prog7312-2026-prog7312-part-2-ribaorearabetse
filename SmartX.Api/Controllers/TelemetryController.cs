using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Models;
using SmartX.Api.Services;
using SmartX.Shared.Contracts;
using SmartX.Shared.Telemetry;

namespace SmartX.Api.Controllers;

/// <summary>
/// Telemetry ingestion, split into one endpoint set per sensor category. JSON cannot express an
/// open type parameter, so a single endpoint could never accept "any T" from the wire. Resolving
/// the type by category instead means each endpoint closes <see cref="TelemetryPacket{T}"/> over a
/// concrete value type at compile time, keeping the whole path boxing-free while still exercising
/// the one generic wrapper across float, int and bool.
/// </summary>
[ApiController]
[Authorize]
[Route("api/telemetry")]
public class TelemetryController(SmartXStore store, RealtimeStructures realtime, TelemetryQueueProcessor processor) : ControllerBase
{
    [HttpPost("environmental")]
    public ActionResult PostEnvironmental([FromBody] ReadingRequest<float> request, [FromQuery] bool drain = true)
    {
        var rejection = Reject(request.SensorId, SensorCategory.Environmental, out var sensor);
        if (rejection is not null)
        {
            return rejection;
        }

        realtime.EnqueueReading(sensor, request.Value);
        if (drain)
        {
            processor.DrainPending();
        }

        return Ok(new { sensorId = sensor.MacAddress, queued = true, critical = RealtimeStructures.IsCritical(sensor.Category, request.Value) });
    }

    [HttpPost("power")]
    public ActionResult PostPower([FromBody] ReadingRequest<int> request, [FromQuery] bool drain = true)
    {
        var rejection = Reject(request.SensorId, SensorCategory.PowerConsumption, out var sensor);
        if (rejection is not null)
        {
            return rejection;
        }

        realtime.EnqueueReading(sensor, request.Value);
        if (drain)
        {
            processor.DrainPending();
        }

        return Ok(new { sensorId = sensor.MacAddress, queued = true, critical = RealtimeStructures.IsCritical(sensor.Category, request.Value) });
    }

    [HttpPost("actuator")]
    public ActionResult PostActuator([FromBody] ReadingRequest<bool> request)
    {
        var rejection = Reject(request.SensorId, SensorCategory.Actuator, out var sensor);
        if (rejection is not null)
        {
            return rejection;
        }

        realtime.EnqueueReading(sensor, request.Value ? 1 : 0);
        processor.DrainPending();
        return Ok(new { sensorId = sensor.MacAddress, queued = true, critical = false });
    }

    [HttpPost("environmental/{sensorId}/batches")]
    public ActionResult<object> PostEnvironmentalBatch(string sensorId, [FromBody] float[] readings)
    {
        var rejection = Reject(sensorId, SensorCategory.Environmental, readings);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!store.TryGetSensor(sensorId, out var sensor))
        {
            return NotFound(new { error = $"Sensor '{sensorId}' is not registered." });
        }

        foreach (var reading in readings)
        {
            realtime.EnqueueReading(sensor, reading);
        }

        processor.DrainPending();
        return Ok(new { sensorId, readingsAccepted = readings.Length });
    }

    [HttpPost("power/{sensorId}/batches")]
    public ActionResult<object> PostPowerBatch(string sensorId, [FromBody] int[] readings)
    {
        var rejection = Reject(sensorId, SensorCategory.PowerConsumption, readings);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!store.TryGetSensor(sensorId, out var sensor))
        {
            return NotFound(new { error = $"Sensor '{sensorId}' is not registered." });
        }

        foreach (var reading in readings)
        {
            realtime.EnqueueReading(sensor, reading);
        }

        processor.DrainPending();
        return Ok(new { sensorId, readingsAccepted = readings.Length });
    }

    [HttpPost("actuator/{sensorId}/batches")]
    public ActionResult<object> PostActuatorBatch(string sensorId, [FromBody] bool[] readings)
    {
        var rejection = Reject(sensorId, SensorCategory.Actuator, readings);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!store.TryGetSensor(sensorId, out var sensor))
        {
            return NotFound(new { error = $"Sensor '{sensorId}' is not registered." });
        }

        foreach (var reading in readings)
        {
            realtime.EnqueueReading(sensor, reading ? 1 : 0);
        }

        processor.DrainPending();
        return Ok(new { sensorId, readingsAccepted = readings.Length });
    }

    [HttpGet("environmental/{sensorId}/history")]
    public ActionResult<List<TelemetryPacket<float>>> GetEnvironmentalHistory(string sensorId)
    {
        var rejection = Reject(sensorId, SensorCategory.Environmental);
        return rejection ?? Ok(store.EnvironmentalHistory(sensorId));
    }

    [HttpGet("power/{sensorId}/history")]
    public ActionResult<List<TelemetryPacket<int>>> GetPowerHistory(string sensorId)
    {
        var rejection = Reject(sensorId, SensorCategory.PowerConsumption);
        return rejection ?? Ok(store.PowerHistory(sensorId));
    }

    [HttpGet("actuator/{sensorId}/history")]
    public ActionResult<List<TelemetryPacket<bool>>> GetActuatorHistory(string sensorId)
    {
        var rejection = Reject(sensorId, SensorCategory.Actuator);
        return rejection ?? Ok(store.ActuatorHistory(sensorId));
    }

    /// <summary>
    /// Confirms the reading belongs to a sensor that is both registered and registered under the
    /// category it is publishing to. A mismatch is a 400 rather than a 404 because the sensor
    /// exists; it is the routing of its data that is wrong.
    /// </summary>
    private ActionResult? Reject(string sensorId, SensorCategory expectedCategory) =>
        Reject(sensorId, expectedCategory, out _);

    private ActionResult? Reject(string sensorId, SensorCategory expectedCategory, out SensorRecord sensor)
    {
        sensor = null!;

        if (string.IsNullOrWhiteSpace(sensorId))
        {
            return BadRequest(new { error = "A telemetry reading must reference a registered sensor." });
        }

        if (!realtime.TryGetDevice(sensorId, out sensor) && !store.TryGetSensor(sensorId, out sensor))
        {
            return NotFound(new { error = $"Sensor '{sensorId}' is not registered." });
        }

        realtime.IndexDevice(sensor);

        if (sensor.Category != expectedCategory)
        {
            return BadRequest(new
            {
                error = $"Sensor '{sensorId}' is registered as {sensor.Category} and cannot publish to the {expectedCategory} endpoint."
            });
        }

        return null;
    }

    private ActionResult? Reject<T>(string sensorId, SensorCategory expectedCategory, T[] readings) where T : struct
    {
        if (readings is null)
        {
            return BadRequest(new { error = "A batch must contain a readings array, even if it is empty." });
        }

        return Reject(sensorId, expectedCategory);
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

