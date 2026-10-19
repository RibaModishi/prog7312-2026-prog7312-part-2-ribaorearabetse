using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SmartX.Api.Services;

/// <summary>
/// Accepts a fixed bearer token only when SmartX:AllowTestAuth is turned on for the automated
/// smoke script. The product login is Firebase; this handler is not registered otherwise.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Token = "test-operator";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (header.ToString() != $"Bearer {Token}")
        {
            return Task.FromResult(AuthenticateResult.Fail("Unrecognised test token."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "ci-operator")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
