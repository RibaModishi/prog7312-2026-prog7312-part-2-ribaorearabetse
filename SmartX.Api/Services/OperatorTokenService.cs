using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SmartX.Api.Services;

/// <summary>
/// Issues the short-lived operator JWT used by the MAUI client. One seeded demo account is enough
/// for Part 1; Part 2 can replace this with a real identity store without touching the controllers.
/// </summary>
public class OperatorTokenService(IConfiguration configuration)
{
    public bool TryIssue(string? username, string? password, out string? token)
    {
        token = null;

        var expectedUser = configuration["DemoOperator:Username"] ?? "operator";
        var expectedPassword = configuration["DemoOperator:Password"] ?? "SmartX-Demo!";

        if (!string.Equals(username?.Trim(), expectedUser, StringComparison.Ordinal) ||
            !string.Equals(password, expectedPassword, StringComparison.Ordinal))
        {
            return false;
        }

        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "SmartX.Api";
        var audience = configuration["Jwt:Audience"] ?? "SmartX.Client";
        var lifetime = TimeSpan.FromMinutes(int.TryParse(configuration["Jwt:ExpiresMinutes"], out var minutes) ? minutes : 480);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim(ClaimTypes.Name, expectedUser)],
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return true;
    }
}
