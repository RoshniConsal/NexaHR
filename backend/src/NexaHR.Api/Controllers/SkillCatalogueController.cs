using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.CvMatching;
using NexaHR.Application.Interfaces.CvMatching;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.HRManager)]
[Route("api/v1/skill-catalogue")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class SkillCatalogueController
    : ControllerBase
{
    private readonly ISkillCatalogueService _service;

    public SkillCatalogueController(
        ISkillCatalogueService service)
    {
        _service = service;
    }

    private Guid GetActorUserId()
    {
        return Guid.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier),
            out var actorUserId)
            ? actorUserId
            : Guid.Empty;
    }

    // API-090
    [HttpGet]
    public async Task<IActionResult> GetCurrent(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetCurrentAsync(
            GetActorUserId(),
            cancellationToken);

        if (!result.Success || result.Data is null)
            return MapFailure(
                result.Code,
                result.Message);

        return Ok(result.Data);
    }

    // API-091
    [HttpPut]
    [RequestSizeLimit(524288)]
    public async Task<IActionResult> Publish(
        [FromBody] PublishSkillCatalogueRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.PublishAsync(
            GetActorUserId(),
            request,
            cancellationToken);

        if (!result.Success || result.Data is null)
            return MapFailure(
                result.Code,
                result.Message);

        return Ok(result.Data);
    }

    private IActionResult MapFailure(
        string code,
        string message)
    {
        var response = new
        {
            code,
            message
        };

        return code switch
        {
            "forbidden" =>
                StatusCode(403, response),

            "invalid_catalogue" =>
                BadRequest(response),

            "catalogue_version_conflict" or
            "catalogue_no_change" =>
                Conflict(response),

            "catalogue_integrity_error" or
            "catalogue_database_error" =>
                StatusCode(503, response),

            _ => StatusCode(500, response)
        };
    }
}
