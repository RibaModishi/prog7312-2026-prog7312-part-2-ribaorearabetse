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
