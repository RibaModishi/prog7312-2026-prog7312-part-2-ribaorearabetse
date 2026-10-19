using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Services;
using SmartX.Shared.Attachments;
using SmartX.Shared.Contracts;
using SmartX.Shared.Deployment;
using SmartX.Shared.Telemetry;

namespace SmartX.Api.Controllers;

/// <summary>
/// Sensor registration and deployment topology. Registration is the point where the recursive
/// validator earns its keep: a sensor is only accepted if grafting it onto the estate leaves a
/// structurally sound tree, and the validator's own messages are handed straight back to the
/// caller so the dashboard never has to invent its own wording for a structural fault.
/// </summary>
[ApiController]
[Authorize]
[Route("api/sensors")]
public class SensorsController(SmartXStore store, RealtimeStructures realtime, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    public ActionResult<SensorRecord> Register([FromBody] SensorRegistrationRequest request)
    {
        var result = store.RegisterSensor(request);

        if (!result.IsSuccess)
        {
            return BadRequest(new { violations = result.Violations });
        }

        realtime.IndexDevice(result.Sensor!);
        return CreatedAtAction(nameof(GetById), new { sensorId = result.Sensor!.MacAddress }, result.Sensor);
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<SensorRecord>> GetAll() => Ok(store.Sensors);

    [HttpGet("{sensorId}")]
    public ActionResult<SensorRecord> GetById(string sensorId) =>
        store.TryGetSensor(sensorId, out var sensor)
            ? Ok(sensor)
            : NotFound(new { error = $"Sensor '{sensorId}' is not registered." });

    [HttpGet("/api/deployment-tree")]
    public ActionResult<DeploymentNode> GetDeploymentTree() => Ok(store.DeploymentTree);

    /// <summary>
    /// Attaches a configuration file, deployment photo or hardware log to a registered sensor.
    /// The upload is streamed straight to disk rather than buffered into memory, so a large
    /// hardware log does not cost the gateway a matching allocation.
    /// </summary>
    [HttpPost("{sensorId}/attachments")]
    public async Task<ActionResult<object>> UploadAttachment(string sensorId, IFormFile file)
    {
        if (!store.TryGetSensor(sensorId, out _))
        {
            return NotFound(new { error = $"Sensor '{sensorId}' is not registered." });
        }

        // Only the bare file name is kept, so a crafted name like "../../appsettings.json" cannot
        // escape the attachments folder.
        var submittedName = Path.GetFileName(file?.FileName);
        if (!AttachmentRules.TryValidate(submittedName, file?.ContentType, file?.Length ?? 0, out var error))
        {
            return BadRequest(new { error });
        }

        var folder = Path.Combine(AttachmentsRoot(), SafeFolderName(sensorId));
        Directory.CreateDirectory(folder);

        // MAC addresses and repeat uploads both collide easily, so each save is prefixed with the
        // capture time to keep earlier deployment photos intact.
        var savedName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{submittedName}";
        var savedPath = Path.Combine(folder, savedName);

        await using (var destination = System.IO.File.Create(savedPath))
        {
            await file!.CopyToAsync(destination);
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(savedPath);
        var summary = AttachmentSummariser.Summarise(savedName, bytes);
        store.RememberAttachment(sensorId, summary);

        return Ok(new
        {
            sensorId,
            savedName,
            sizeInBytes = summary.SizeInBytes,
            kind = summary.Kind,
            preview = summary.Preview,
            lineCount = summary.LineCount,
            fieldCount = summary.FieldCount
        });
    }

    [HttpPost("import")]
    public async Task<ActionResult<object>> ImportDevices(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Choose a .txt file with one device per line." });
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Device import expects a .txt file." });
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var loaded = new List<string>();
        var errors = new List<string>();
        var lineNumber = 0;

        while (await reader.ReadLineAsync() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 5)
            {
                errors.Add($"Line {lineNumber} must be MAC|Name|Category|Facility|Zone.");
                continue;
            }

            if (!Enum.TryParse<SensorCategory>(parts[2].Trim(), ignoreCase: true, out var category))
            {
                errors.Add($"Line {lineNumber} has an unknown category '{parts[2].Trim()}'.");
                continue;
            }

            var result = store.RegisterSensor(new SensorRegistrationRequest
            {
                MacAddress = parts[0].Trim(),
                DisplayName = parts[1].Trim(),
                Category = category,
                FacilityName = parts[3].Trim(),
                ZoneName = parts[4].Trim()
            });

            if (!result.IsSuccess)
            {
                errors.Add($"Line {lineNumber}: {string.Join(" ", result.Violations)}");
                continue;
            }

            realtime.IndexDevice(result.Sensor!);
            loaded.Add(result.Sensor!.MacAddress);
        }

        return Ok(new { loaded, errors, deviceCount = realtime.Devices().Count });
    }

    [HttpGet("{sensorId}/attachments")]
    public ActionResult<IReadOnlyList<AttachmentSummary>> GetAttachments(string sensorId) =>
        store.TryGetSensor(sensorId, out _)
            ? Ok(store.AttachmentsFor(sensorId))
            : NotFound(new { error = $"Sensor '{sensorId}' is not registered." });

    private string AttachmentsRoot() =>
        Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "attachments");

    /// <summary>
    /// MAC addresses contain colons, which are illegal in Windows path segments, so anything the
    /// file system rejects is stripped before the identifier is used as a folder name.
    /// </summary>
    private static string SafeFolderName(string sensorId) =>
        string.Concat(sensorId.Split(Path.GetInvalidFileNameChars()));
}
