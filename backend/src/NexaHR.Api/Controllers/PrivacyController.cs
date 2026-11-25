using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Privacy;
using NexaHR.Application.Interfaces.Privacy;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/privacy")]
[Authorize]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class PrivacyController : ControllerBase
{
    private readonly IPrivacyService _privacy;

    public PrivacyController(IPrivacyService privacy)
    {
        _privacy = privacy;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier),
            out actorId)
            && actorId != Guid.Empty;
    }

    private IActionResult ToResult<T>(
        PrivacyOperationResponse<T> result)
        where T : class
    {
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(403, result),

            "not_found" =>
                NotFound(result),

            "version_conflict" or
            "scope_conflict" or
            "policy_not_configured" or
            "retention_integration_pending" =>
                Conflict(result),

            _ => BadRequest(result)
        };
    }

    // API-154: Read processing policy
    [HttpGet("processing-policy")]
    public async Task<IActionResult> GetProcessingPolicy()
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.GetProcessingPolicyAsync(
                actorId);

        return ToResult(result);
    }

    // API-155: Record new processing policy version
    [HttpPut("processing-policy")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> UpdateProcessingPolicy(
        [FromBody] UpdateProcessingPolicyRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.UpdateProcessingPolicyAsync(
                actorId,
                request);

        return ToResult(result);
    }

    // API-156: Retention eligibility preview
    [HttpPost("retention-preview")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> PreviewRetention(
        [FromBody] RetentionPreviewRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.PreviewRetentionAsync(
                actorId,
                request);

        return ToResult(result);
    }

    // API-157: Explicit retention run authorization
    [HttpPost("retention-runs")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> CreateRetentionRun(
        [FromBody] CreateRetentionRunRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.CreateRetentionRunAsync(
                actorId,
                request);

        if (result.Success && result.Data is not null)
        {
            return AcceptedAtAction(
                nameof(GetRetentionRun),
                new { runId = result.Data.RunId },
                result);
        }

        return ToResult(result);
    }

    // API-158: Retention run status
    [HttpGet("retention-runs/{runId:guid}")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> GetRetentionRun(
        Guid runId)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.GetRetentionRunAsync(
                actorId,
                runId);

        return ToResult(result);
    }

    // API-159: Place or update legal hold
    [HttpPut("legal-holds/{subjectId:guid}")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> UpsertLegalHold(
        Guid subjectId,
        [FromBody] UpsertLegalHoldRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.UpsertLegalHoldAsync(
                actorId,
                subjectId,
                request);

        return ToResult(result);
    }

    // API-160: Release hold without deleting history
    [HttpDelete("legal-holds/{subjectId:guid}")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> ReleaseLegalHold(
        Guid subjectId,
        [FromBody] ReleaseLegalHoldRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _privacy.ReleaseLegalHoldAsync(
                actorId,
                subjectId,
                request);

        return ToResult(result);
    }
}
