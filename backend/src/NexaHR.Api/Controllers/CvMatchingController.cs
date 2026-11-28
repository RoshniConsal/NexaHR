using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.Interfaces.CvMatching;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.HRManager)]
[Route("api/v1")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class CvMatchingController : ControllerBase
{
    private readonly ICvMatchingService _service;

    public CvMatchingController(
        ICvMatchingService service)
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

    // API-087
    [HttpPost(
        "applications/{applicationId:guid}/matches")]
    public async Task<IActionResult> Create(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            GetActorUserId(),
            applicationId,
            cancellationToken);

        if (!result.Success || result.Data is null)
            return MapFailure(
                result.Code,
                result.Message);

        return CreatedAtAction(
            nameof(GetResult),
            new { matchId = result.Data.Id },
            result.Data);
    }

    // API-088
    [HttpGet(
        "applications/{applicationId:guid}/matches")]
    public async Task<IActionResult> GetHistory(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetHistoryAsync(
            GetActorUserId(),
            applicationId,
            cancellationToken);

        if (!result.Success || result.Data is null)
            return MapFailure(
                result.Code,
                result.Message);

        return Ok(result.Data);
    }

    // API-089
    [HttpGet("matches/{matchId:guid}")]
    public async Task<IActionResult> GetResult(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetResultAsync(
            GetActorUserId(),
            matchId,
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

            "invalid_application" or
            "invalid_match" =>
                BadRequest(response),

            "application_not_found" or
            "match_not_found" =>
                NotFound(response),

            "confirmed_review_required" or
            "revision_not_approved" or
            "skill_catalogue_required" or
            "matching_write_conflict" or
            "duplicate_skill_criteria" or
            "invalid_matching_source" or
            "invalid_catalogue" =>
                Conflict(response),

            "document_not_available" or
            "catalogue_integrity_error" or
            "matching_database_error" =>
                StatusCode(503, response),

            _ => StatusCode(500, response)
        };
    }
}
