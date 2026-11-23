using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Profile;
using NexaHR.Application.Interfaces.Profile;
using System.Security.Claims;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(
        IProfileService profileService)
    {
        _profileService = profileService;
    }

    // API-009: Get own profile
    [HttpGet]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
            return Unauthorized();

        var profile =
            await _profileService.GetMyProfileAsync(userId);

        if (profile is null)
            return Unauthorized();

        return Ok(profile);
    }

    // API-010: Update own profile
    [HttpPatch]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest request)
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
            return Unauthorized();

        var result =
            await _profileService.UpdateMyProfileAsync(
                userId, request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
