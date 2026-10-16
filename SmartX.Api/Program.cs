using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Categories and node types travel as their names rather than ordinals, so a payload
        // stays readable in Swagger and a reordered enum cannot silently change its meaning.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// The gateway keeps the deployment tree and all accumulated telemetry in process, so the store
// must be a singleton; a scoped registration would discard every registered sensor per request.
builder.Services.AddSingleton<SmartXStore>();
builder.Services.AddSingleton<AlertAggregationService>();
builder.Services.AddSingleton<OperatorTokenService>();
builder.Services.AddHostedService<SensorDisconnectWatcher>();
builder.Services.AddHostedService<SensorTelemetrySimulator>();

var app = builder.Build();

// Local development is HTTP-only (see MauiProgram / Android emulator trust). HTTPS
// redirection would bounce the Swagger UI's fetch of /openapi/v1.json onto a port that
// is not listening under the "http" launch profile, which shows up as
// "Failed to load API definition".
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
