using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Services;
using SmartX.Shared.Realtime;
using Structures = SmartX.Api.Services.RealtimeStructures;
using SmartX.Shared.Telemetry;

namespace SmartX.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/commands")]
public class CommandsController(Structures realtime, PredictiveEngine predictive) : ControllerBase
{
    [HttpPost]
    public ActionResult<object> Issue([FromBody] CommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SensorId) || string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest(new { error = "A command needs a sensor and an action." });
        }

        if (!realtime.TryGetDevice(request.SensorId, out var sensor))
        {
            return NotFound(new { error = $"Sensor '{request.SensorId}' is not registered." });
        }

        if (sensor.Category != SensorCategory.Actuator)
        {
            return BadRequest(new { error = "Override commands apply to actuator devices such as pumps and relays." });
        }

        var action = request.Action.Trim();
        var inverse = InverseOf(action);
        if (inverse is null)
        {
            return BadRequest(new { error = $"Unknown action '{action}'. Use StartPump, StopPump, or ToggleRelay." });
        }

        var nearbyMoisture = realtime.Devices()
            .Where(device => device.Category == SensorCategory.Environmental)
            .Select(device => realtime.LastValue(device.MacAddress))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .DefaultIfEmpty(double.NaN)
            .Min();

        var command = new DeviceCommand
        {
            SensorId = sensor.MacAddress,
            Action = action,
            InverseAction = inverse
        };

        realtime.PushCommand(command);
        predictive.RecordCommand(
            sensor.MacAddress,
            action,
            double.IsNaN(nearbyMoisture) ? null : nearbyMoisture,
            DateTime.UtcNow);

        return Ok(new { sensorId = sensor.MacAddress, action, undoDepth = realtime.Depths().UndoDepth });
    }

    [HttpPost("undo")]
    public ActionResult<object> Undo()
    {
        if (!realtime.TryUndo(out var undone, out var message))
        {
            return Ok(new { undone = false, message });
        }

        return Ok(new { undone = true, message, sensorId = undone!.SensorId, action = undone.InverseAction });
    }

    private static string? InverseOf(string action) => action switch
    {
        "StartPump" => "StopPump",
        "StopPump" => "StartPump",
        "ToggleRelay" => "ToggleRelay",
        _ => null
    };
}

public sealed class CommandRequest
{
    public string? SensorId { get; set; }

    public string? Action { get; set; }
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

