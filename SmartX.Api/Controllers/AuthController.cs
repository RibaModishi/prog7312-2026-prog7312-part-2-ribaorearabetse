using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Services;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(OperatorTokenService tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public ActionResult<object> Login([FromBody] LoginRequest request)
    {
        if (!tokens.TryIssue(request.Username, request.Password, out var token))
        {
            return Unauthorized(new { error = "Username or password is not recognised." });
        }

        return Ok(new { token });
    }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}
