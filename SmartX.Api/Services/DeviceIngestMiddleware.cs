using System.Security.Claims;

namespace SmartX.Api.Services;

/// <summary>
/// Lets the in-process simulator call the public telemetry endpoints the way a device would,
/// without a person's Firebase password. The key exists only in this process.
/// </summary>
public class DeviceIngestMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-SmartX-Device";

    public static string Key { get; } = Guid.NewGuid().ToString("N");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var presented)
            && presented == Key)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "smartx-device")],
                authenticationType: "SmartXDevice");
            context.User = new ClaimsPrincipal(identity);
        }

        await next(context);
    }
}
