using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Auth;
using NexaHR.Application.Interfaces.Auth;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // API-001: Public candidate registration
    [HttpPost("register")]
    [EnableRateLimiting("auth-sensitive")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        var result =
            await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return StatusCode(
            StatusCodes.Status201Created, result);
    }

    // API-002: User login
    [HttpPost("login")]
    [EnableRateLimiting("auth-sensitive")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var result =
            await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return Unauthorized(new
            {
                message = result.Message
            });
        }

        Response.Headers["Cache-Control"] = "no-store";

        return Ok(result);
    }


    // API-003: Revoke current session
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        if (!Guid.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var userId) ||
            !Guid.TryParse(
                (User.FindFirst("sid")?.Value ?? User.FindFirst(ClaimTypes.Sid)?.Value),
                out var sessionId))
        {
            return Unauthorized();
        }

        await _authService.LogoutAsync(
            userId, sessionId);

        return NoContent();
    }

    // API-004: Rotate refresh token
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-sensitive")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request)
    {
        Response.Headers["Cache-Control"] = "no-store";

        var result =
            await _authService.RefreshAsync(request);

        if (!result.Success)
        {
            return Unauthorized(new
            {
                message = result.Message
            });
        }

        return Ok(result);
    }


    // API-005: Verify email
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request)
    {
        var result =
            await _authService.VerifyEmailAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // API-006: Resend verification
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] EmailRequest request)
    {
        var result =
            await _authService.ResendVerificationAsync(request);

        return Accepted(result);
    }

    // API-007: Forgot password
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] EmailRequest request)
    {
        var result =
            await _authService.ForgotPasswordAsync(request);

        return Accepted(result);
    }

    // API-008: Reset password
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        var result =
            await _authService.ResetPasswordAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

}
