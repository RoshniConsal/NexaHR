using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Company;
using NexaHR.Application.Interfaces.Company;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/company")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class CompanyController : ControllerBase
{
    private readonly ICompanySettingsService _service;

    public CompanyController(
        ICompanySettingsService service)
    {
        _service = service;
    }

    private Guid? GetUserId()
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : null;
    }

    // API-020
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var userId = GetUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var result = await _service.GetSettingsAsync(
            userId.Value);

        return result is null
            ? Forbid()
            : Ok(result);
    }

    // API-021
    [HttpPut("settings")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> UpdateSettings(
        UpdateCompanySettingsRequest request)
    {
        var userId = GetUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var result = await _service.UpdateSettingsAsync(
            userId.Value,
            request);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "concurrency_conflict" => Conflict(result),

            _ => BadRequest(result)
        };
    }

    // API-022
    [HttpGet("support-contact")]
    public async Task<IActionResult> GetSupportContact()
    {
        var userId = GetUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var result = await _service.GetSupportContactAsync(
            userId.Value);

        return result is null
            ? Forbid()
            : Ok(result);
    }
}
