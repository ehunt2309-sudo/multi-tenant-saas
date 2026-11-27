using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.API.Api.Configuration;
using Platform.API.Api.Middleware;
using Platform.API.Modules.Accounts;
using Platform.API.Shared.Utilities;

namespace Platform.API.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Customer self-registration for the venue whose website is calling.</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        Ok(await auth.RegisterCustomerAsync(request, HttpContext.ClientIp(), ct));

    /// <summary>Login for customers, staff and managers. With no venue resolved it is a platform-admin login.</summary>
    [HttpPost("login"), AllowAnonymous, AllowNoTenant, EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, HttpContext.ClientIp(), ct));

    [HttpPost("refresh"), AllowAnonymous, AllowNoTenant, EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await auth.RefreshAsync(request.RefreshToken, HttpContext.ClientIp(), ct));

    [HttpPost("logout"), AllowAnonymous, AllowNoTenant]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, HttpContext.ClientIp(), ct);
        return NoContent();
    }

    [HttpPost("change-password"), AllowNoTenant]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, HttpContext.ClientIp(), ct);
        return NoContent();
    }

    [HttpGet("me"), AllowNoTenant]
    public async Task<ActionResult<UserInfo>> Me(CancellationToken ct) => Ok(await auth.GetMeAsync(ct));
}
