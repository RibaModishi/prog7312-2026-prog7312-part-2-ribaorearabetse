using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var projectId = builder.Configuration["Firebase:ProjectId"];
var allowTestAuth = builder.Configuration.GetValue("SmartX:AllowTestAuth", false);

var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = allowTestAuth
        ? TestAuthHandler.SchemeName
        : JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = options.DefaultAuthenticateScheme;
});

if (allowTestAuth)
{
    authentication.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
}
else
{
    authentication.AddJwtBearer(options =>
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            options.TokenValidationParameters.ValidateIssuer = false;
            options.TokenValidationParameters.ValidateAudience = false;
            options.TokenValidationParameters.ValidateIssuerSigningKey = false;
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = _ => throw new InvalidOperationException("Firebase:ProjectId is not configured.")
            };
            return;
        }

        options.Authority = $"https://securetoken.google.com/{projectId}";
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = $"https://securetoken.google.com/{projectId}";
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = projectId;
        options.TokenValidationParameters.ValidateLifetime = true;
    });
}

builder.Services.AddAuthorization();

builder.Services.AddSingleton<SmartXStore>();
builder.Services.AddSingleton<AlertAggregationService>();
builder.Services.AddSingleton<RealtimeStructures>();
builder.Services.AddSingleton<SmartX.Shared.Realtime.PredictiveEngine>();
builder.Services.AddSingleton<TelemetryQueueProcessor>();
builder.Services.AddHttpClient("device-ingest", client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:5022/");
    client.DefaultRequestHeaders.Add(DeviceIngestMiddleware.HeaderName, DeviceIngestMiddleware.Key);
});
builder.Services.AddHostedService(provider => provider.GetRequiredService<TelemetryQueueProcessor>());
builder.Services.AddHostedService<SensorDisconnectWatcher>();
builder.Services.AddHostedService<SensorTelemetrySimulator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Smart-X Gateway API");
        options.RoutePrefix = "swagger";
    });
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<DeviceIngestMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.Run();


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

