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
app.UseAuthorization();
app.MapControllers();
app.Run();
