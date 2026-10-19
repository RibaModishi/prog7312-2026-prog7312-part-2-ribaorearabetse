using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartX.Shared.Contracts;
using SmartX.Shared.Realtime;

namespace SmartX.Client.Services;

/// <summary>
/// Talks to the Smart-X gateway. Every failure the user can plausibly hit — an unreachable API, a
/// rejected registration, a malformed response — comes back as an outcome rather than an
/// exception, so the UI has one code path to render instead of a try/catch around every call.
/// </summary>
public class SmartXApiClient(HttpClient httpClient, OperatorSession session)
{
    private string? _accessToken;

    // The gateway serialises enums by name, so the client has to read and write them the same way
    // or SensorCategory round-trips as a number the API cannot match.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<SensorRegistrationOutcome> RegisterSensorAsync(SensorRegistrationRequest request)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/sensors", request, JsonOptions);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var sensor = await response.Content.ReadFromJsonAsync<SensorRecord>(JsonOptions);

                return sensor is null
                    ? SensorRegistrationOutcome.Failed(["The gateway accepted the sensor but returned no record."])
                    : SensorRegistrationOutcome.Succeeded(sensor);
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                var body = await response.Content.ReadAsStringAsync();
                return SensorRegistrationOutcome.Failed(ExtractViolations(body));
            }

            return SensorRegistrationOutcome.Failed(
                [$"The gateway responded with {(int)response.StatusCode} {response.ReasonPhrase}."]);
        }
        catch (HttpRequestException)
        {
            return SensorRegistrationOutcome.Failed(
                [$"Could not reach the Smart-X gateway at {httpClient.BaseAddress}. Check the API is running."]);
        }
        catch (TaskCanceledException)
        {
            return SensorRegistrationOutcome.Failed(["The gateway did not respond in time."]);
        }
        catch (JsonException)
        {
            return SensorRegistrationOutcome.Failed(["The gateway returned a response the client could not read."]);
        }
    }

    /// <summary>
    /// Snapshot of the currently open alert groups. An empty list is a successful read — it means
    /// the mesh is quiet, not that the gateway failed — so the dashboard can show an empty state
    /// without treating silence as an error.
    /// </summary>
    public async Task<AlertDashboardOutcome> GetActiveAlertsAsync()
    {
        try
        {
            using var response = await httpClient.GetAsync("api/alerts");

            if (!response.IsSuccessStatusCode)
            {
                return AlertDashboardOutcome.Failed(
                    $"The gateway responded with {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var groups = await response.Content.ReadFromJsonAsync<List<AlertGroup>>(JsonOptions);

            return AlertDashboardOutcome.Succeeded(groups ?? []);
        }
        catch (HttpRequestException)
        {
            return AlertDashboardOutcome.Failed(
                $"Could not reach the Smart-X gateway at {httpClient.BaseAddress}. Check the API is running.");
        }
        catch (TaskCanceledException)
        {
            return AlertDashboardOutcome.Failed("The gateway did not respond in time.");
        }
        catch (JsonException)
        {
            return AlertDashboardOutcome.Failed("The gateway returned a response the client could not read.");
        }
    }

    /// <summary>
    /// Uploads a configuration file, deployment photo or hardware log against a registered sensor.
    /// The file is streamed rather than read into a byte array, so attaching a large hardware log
    /// does not cost the device a matching allocation.
    /// </summary>
    public async Task<AttachmentUploadOutcome> UploadAttachmentAsync(string sensorId, FileResult file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = await file.OpenReadAsync();
            using var filePart = new StreamContent(stream);

            filePart.Headers.ContentType = ResolveContentType(file);

            // The part name has to be "file" because that is what the IFormFile parameter on
            // SensorsController.UploadAttachment is called. Any other name still uploads cleanly
            // and the server simply binds null, which is a genuinely confusing way to fail.
            content.Add(filePart, "file", file.FileName);

            using var response = await httpClient.PostAsync($"api/sensors/{Uri.EscapeDataString(sensorId)}/attachments", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return AttachmentUploadOutcome.Failed(ExtractError(body, response.StatusCode));
            }

            var saved = JsonSerializer.Deserialize<AttachmentPayload>(body, JsonOptions);

            return string.IsNullOrWhiteSpace(saved?.SavedName)
                ? AttachmentUploadOutcome.Failed("The gateway stored the file but did not say under what name.")
                : AttachmentUploadOutcome.Succeeded(saved.SavedName, saved.Preview);
        }
        catch (HttpRequestException)
        {
            return AttachmentUploadOutcome.Failed(
                $"Could not reach the Smart-X gateway at {httpClient.BaseAddress}. Check the API is running.");
        }
        catch (TaskCanceledException)
        {
            return AttachmentUploadOutcome.Failed("The upload did not complete in time.");
        }
        catch (JsonException)
        {
            return AttachmentUploadOutcome.Failed("The gateway returned a response the client could not read.");
        }
        catch (IOException)
        {
            return AttachmentUploadOutcome.Failed($"'{file.FileName}' could not be read from this device.");
        }
        catch (UnauthorizedAccessException)
        {
            return AttachmentUploadOutcome.Failed($"This app is not allowed to read '{file.FileName}'.");
        }
    }

    /// <summary>
    /// A picked file does not always carry a usable content type, and a malformed one would throw
    /// while building the request rather than being reported as a failed upload.
    /// </summary>
    private static MediaTypeHeaderValue ResolveContentType(FileResult file) =>
        MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType)
            ? contentType
            : new MediaTypeHeaderValue("application/octet-stream");

    private static string ExtractError(string body, HttpStatusCode statusCode)
    {
        var messages = ExtractMessages(body);

        return messages.Count > 0
            ? string.Join(" ", messages)
            : $"The gateway rejected the upload with {(int)statusCode} {statusCode}.";
    }

    private static IReadOnlyList<string> ExtractViolations(string body)
    {
        var messages = ExtractMessages(body);

        return messages.Count > 0
            ? messages
            : ["The gateway rejected the registration without explaining why."];
    }

    /// <summary>
    /// A refusal arrives in one of several shapes. The validator answers with
    /// <c>{ violations: [...] }</c> and the attachment endpoint with <c>{ error: "..." }</c>, but a
    /// request that fails model binding never reaches either and is answered with ASP.NET's own
    /// ProblemDetails. Reporting nothing in that last case would hide a real bug — a mismatched
    /// multipart part name shows up there and nowhere else — so every shape is unpacked.
    /// </summary>
    private static IReadOnlyList<string> ExtractMessages(string body)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<RejectionPayload>(body, JsonOptions);

            if (payload?.Violations is { Count: > 0 } violations)
            {
                return violations;
            }

            if (!string.IsNullOrWhiteSpace(payload?.Error))
            {
                return [payload.Error];
            }

            if (payload?.Errors is { Count: > 0 } errors)
            {
                return errors.SelectMany(field => field.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(payload?.Title))
            {
                return [payload.Title];
            }
        }
        catch (JsonException)
        {
            // Falls through to the caller's own fallback message.
        }

        return [];
    }

    private sealed class RejectionPayload
    {
        public List<string>? Violations { get; set; }

        public string? Title { get; set; }

        public Dictionary<string, List<string>>? Errors { get; set; }

        public string? Error { get; set; }
    }

    private sealed class AttachmentPayload
    {
        public string? SavedName { get; set; }

        public string? Preview { get; set; }
    }

    public bool HasAccessToken => !string.IsNullOrWhiteSpace(_accessToken);

    public void SetAccessToken(string? token)
    {
        _accessToken = token;
        session.AccessToken = token;
        httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<LoginOutcome> LoginAsync(string email, string password)
    {
        if (!FirebaseConfig.IsConfigured)
        {
            return LoginOutcome.Failed(
                "Firebase is not configured. Set FirebaseConfig.ApiKey and ProjectId to the class Firebase project.");
        }

        try
        {
            using var firebase = new HttpClient();
            using var response = await firebase.PostAsJsonAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={FirebaseConfig.ApiKey}",
                new { email, password, returnSecureToken = true });

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return LoginOutcome.Failed(ReadFirebaseError(body));
            }

            var payload = JsonSerializer.Deserialize<FirebaseSignInPayload>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(payload?.IdToken))
            {
                return LoginOutcome.Failed("Firebase signed in but did not return an ID token.");
            }

            SetAccessToken(payload.IdToken);
            return LoginOutcome.Succeeded();
        }
        catch (HttpRequestException)
        {
            return LoginOutcome.Failed("Could not reach Firebase Authentication.");
        }
        catch (TaskCanceledException)
        {
            return LoginOutcome.Failed("Firebase did not respond in time.");
        }
        catch (JsonException)
        {
            return LoginOutcome.Failed("Firebase returned a response the client could not read.");
        }
    }

    public async Task<CommandStreamOutcome> GetCommandStreamAsync(string? category, string? alertState, string? query)
    {
        var path = "api/command-stream?category=" + Uri.EscapeDataString(category ?? "all")
            + "&alertState=" + Uri.EscapeDataString(alertState ?? "all")
            + "&q=" + Uri.EscapeDataString(query ?? string.Empty);

        try
        {
            using var response = await httpClient.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return CommandStreamOutcome.Failed(ExtractError(body, response.StatusCode));
            }

            var snapshot = JsonSerializer.Deserialize<CommandStreamSnapshot>(body, JsonOptions);
            return CommandStreamOutcome.Succeeded(snapshot ?? new CommandStreamSnapshot());
        }
        catch (HttpRequestException)
        {
            return CommandStreamOutcome.Failed("Could not reach the command stream.");
        }
        catch (JsonException)
        {
            return CommandStreamOutcome.Failed("The command stream response could not be read.");
        }
    }

    public async Task<CommandStreamOutcome> SearchCommandStreamAsync(string? category, string? alertState, string? query)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "api/command-stream/search",
                new { query, category, alertState },
                JsonOptions);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return CommandStreamOutcome.Failed(ExtractError(body, response.StatusCode));
            }

            var snapshot = JsonSerializer.Deserialize<CommandStreamSnapshot>(body, JsonOptions);
            return CommandStreamOutcome.Succeeded(snapshot ?? new CommandStreamSnapshot());
        }
        catch (HttpRequestException)
        {
            return CommandStreamOutcome.Failed("Could not reach the command stream.");
        }
    }

    public async Task<(bool Ok, string Message)> IssueCommandAsync(string sensorId, string action)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/commands", new { sensorId, action }, JsonOptions);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return (false, ExtractError(body, response.StatusCode));
            }

            return (true, "The device accepted that.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, "The gateway did not answer. Check that the API is still running.");
        }
    }

    public async Task<(bool Ok, string Message)> UndoCommandAsync()
    {
        try
        {
            using var response = await httpClient.PostAsync("api/commands/undo", null);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return (false, ExtractError(body, response.StatusCode));
            }

            using var document = JsonDocument.Parse(body);
            var undone = document.RootElement.TryGetProperty("undone", out var flag) && flag.GetBoolean();
            var message = document.RootElement.TryGetProperty("message", out var text)
                ? text.GetString()
                : null;
            return (undone, message ?? "The last command was undone.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (false, "The gateway did not answer. Check that the API is still running.");
        }
    }

    public async Task<string?> ImportDevicesAsync(FileResult file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = await file.OpenReadAsync();
        using var filePart = new StreamContent(stream);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(filePart, "file", file.FileName);

        using var response = await httpClient.PostAsync("api/sensors/import", content);
        var body = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode ? body : ExtractError(body, response.StatusCode);
    }

    public async Task<int> CountSensorsAsync()
    {
        using var response = await httpClient.GetAsync("api/sensors");
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        var sensors = await response.Content.ReadFromJsonAsync<List<SensorRecord>>(JsonOptions);
        return sensors?.Count ?? 0;
    }

    private static string ReadFirebaseError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                return message.GetString() switch
                {
                    "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "INVALID_LOGIN_CREDENTIALS" =>
                        "Firebase rejected that email or password.",
                    var other when !string.IsNullOrWhiteSpace(other) => other,
                    _ => "Firebase rejected the sign-in."
                };
            }
        }
        catch (JsonException)
        {
        }

        return "Firebase rejected the sign-in.";
    }

    private sealed class FirebaseSignInPayload
    {
        public string? IdToken { get; set; }
    }
}

public class LoginOutcome
{
    private LoginOutcome(string? errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    public string? ErrorMessage { get; }

    public bool IsSuccess => ErrorMessage is null;

    public static LoginOutcome Succeeded() => new(null);

    public static LoginOutcome Failed(string errorMessage) => new(errorMessage);
}

/// <summary>
/// Result of a registration attempt as the UI sees it. Violations are a list because the recursive
/// validator reports every fault in a deployment at once, and collapsing them to a single message
/// would throw away most of what it found.
/// </summary>
public class SensorRegistrationOutcome
{
    private SensorRegistrationOutcome(SensorRecord? sensor, IReadOnlyList<string> violations)
    {
        Sensor = sensor;
        Violations = violations;
    }

    public SensorRecord? Sensor { get; }

    public IReadOnlyList<string> Violations { get; }

    public bool IsSuccess => Sensor is not null;

    public static SensorRegistrationOutcome Succeeded(SensorRecord sensor) => new(sensor, []);

    public static SensorRegistrationOutcome Failed(IReadOnlyList<string> violations) => new(null, violations);
}

/// <summary>
/// Result of an attachment upload. A single message is enough here: unlike a registration, an
/// upload fails for one reason at a time rather than accumulating structural faults.
/// </summary>
public class AttachmentUploadOutcome
{
    private AttachmentUploadOutcome(string? fileName, string? preview, string? errorMessage)
    {
        FileName = fileName;
        Preview = preview;
        ErrorMessage = errorMessage;
    }

    public string? FileName { get; }

    public string? Preview { get; }

    public string? ErrorMessage { get; }

    public bool IsSuccess => FileName is not null;

    public static AttachmentUploadOutcome Succeeded(string fileName, string? preview) => new(fileName, preview, null);

    public static AttachmentUploadOutcome Failed(string errorMessage) => new(null, null, errorMessage);
}

/// <summary>
/// Result of a dashboard poll. Groups are kept as a list even when empty so the UI can tell a
/// quiet mesh apart from a failed fetch without a second null check.
/// </summary>
public class AlertDashboardOutcome
{
    private AlertDashboardOutcome(IReadOnlyList<AlertGroup>? groups, string? errorMessage)
    {
        Groups = groups ?? [];
        ErrorMessage = errorMessage;
    }

    public IReadOnlyList<AlertGroup> Groups { get; }

    public string? ErrorMessage { get; }

    public bool IsSuccess => ErrorMessage is null;

    public static AlertDashboardOutcome Succeeded(IReadOnlyList<AlertGroup> groups) => new(groups, null);

    public static AlertDashboardOutcome Failed(string errorMessage) => new(null, errorMessage);
}

public sealed class CommandStreamSnapshot
{
    public List<CommandStreamDevice> Devices { get; set; } = [];

    public List<SuggestedAction> Suggestions { get; set; } = [];
}

public class CommandStreamOutcome
{
    private CommandStreamOutcome(CommandStreamSnapshot? snapshot, string? errorMessage)
    {
        Snapshot = snapshot ?? new CommandStreamSnapshot();
        ErrorMessage = errorMessage;
    }

    public CommandStreamSnapshot Snapshot { get; }

    public string? ErrorMessage { get; }

    public bool IsSuccess => ErrorMessage is null;

    public static CommandStreamOutcome Succeeded(CommandStreamSnapshot snapshot) => new(snapshot, null);

    public static CommandStreamOutcome Failed(string errorMessage) => new(null, errorMessage);
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

