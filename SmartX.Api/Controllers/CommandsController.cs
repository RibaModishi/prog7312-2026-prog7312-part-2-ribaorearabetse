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
