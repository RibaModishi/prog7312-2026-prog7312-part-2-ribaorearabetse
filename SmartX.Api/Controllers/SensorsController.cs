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

