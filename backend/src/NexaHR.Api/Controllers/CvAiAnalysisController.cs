using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.CvAiAnalysis;
using NexaHR.Application.Interfaces.CvAiAnalysis;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class CvAiAnalysisController : ControllerBase
{
    private readonly ICvAiAnalysisService _service;

    public CvAiAnalysisController(
        ICvAiAnalysisService service)
    {
        _service = service;
    }

    // API-092
    [HttpGet("ai/configuration-status")]
    [Authorize(
        Roles = AppRoles.HRManager + "," + AppRoles.Candidate)]
    public async Task<IActionResult> GetConfigurationStatus(
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var response =
            await _service.GetConfigurationStatusAsync(
                actorId,
                cancellationToken);

        if (!response.Success || response.Data is null)
            return MapResponse(response);

        // The service validates the current database role.
        // Candidate responses expose only the consent notice.
        if (response.Data.ReadinessCode ==
            "consent_notice_only")
        {
            return Ok(new
            {
                response.Success,
                response.Code,
                response.Message,
                Data = new
                {
                    response.Data.ConsentNotice
                }
            });
        }

        // A stale or mismatched token must not expose
        // HR configuration to a Candidate.
        if (!User.IsInRole(AppRoles.HRManager) ||
            User.IsInRole(AppRoles.Candidate))
        {
            return Forbid();
        }

        return Ok(response);
    }

    // API-093
    [HttpGet(
        "applications/{applicationId:guid}/ai-input")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> GetPreparedInput(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var response =
            await _service.GetPreparedInputAsync(
                actorId,
                applicationId,
                cancellationToken);

        return MapResponse(response);
    }

    // API-094
    [HttpPost(
        "applications/{applicationId:guid}/analyses")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> StartAnalysis(
        Guid applicationId,
        [FromBody] StartCvAiAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var response =
            await _service.StartAsync(
                actorId,
                applicationId,
                request,
                cancellationToken);

        if (!response.Success || response.Data is null)
            return MapResponse(response);

        return AcceptedAtAction(
            nameof(GetAnalysisResult),
            new { analysisId = response.Data.Id },
            response);
    }

    // API-095
    [HttpGet(
        "applications/{applicationId:guid}/analyses")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> GetAnalysisHistory(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var response =
            await _service.GetHistoryAsync(
                actorId,
                applicationId,
                cancellationToken);

        return MapResponse(response);
    }

    // API-096
    [HttpGet("analyses/{analysisId:guid}")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> GetAnalysisResult(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var response =
            await _service.GetResultAsync(
                actorId,
                analysisId,
                cancellationToken);

        return MapResponse(response);
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier),
            out actorId)
            && actorId != Guid.Empty;
    }

    private IActionResult MapResponse<T>(
        CvAiOperationResponse<T> response)
        where T : class
    {
        if (response.Success && response.Data is not null)
            return Ok(response);

        var code = response.Code;

        if (code == "forbidden")
            return StatusCode(403, response);

        if (code.EndsWith(
                "_not_found",
                StringComparison.Ordinal))
        {
            return NotFound(response);
        }

        if (code is
            "invalid_application" or
            "invalid_analysis" or
            "invalid_ai_request")
        {
            return BadRequest(response);
        }

        if (code is
            "ai_database_error" or
            "ai_analysis_database_error" or
            "ai_historical_source_invalid" or
            "ai_saved_result_invalid" or
            "invalid_ai_authorization")
        {
            return StatusCode(500, response);
        }

        if (code is
            "ai_disabled" or
            "ai_provider_not_configured" or
            "ai_invalid_model_configuration" or
            "ai_provider_unavailable" or
            "ai_provider_timeout" or
            "ai_provider_read_error" or
            "ai_invalid_provider_response" or
            "ai_model_not_available" or
            "ai_model_digest_mismatch")
        {
            return StatusCode(503, response);
        }

        if (code is
            "ai_consent_required" or
            "ai_consent_revoked" or
            "ai_consent_outdated" or
            "invalid_ai_consent" or
            "invalid_ai_consent_timestamp" or
            "ai_policy_not_enabled" or
            "ai_policy_not_approved" or
            "ai_provider_not_approved" or
            "ai_provider_training_not_permitted")
        {
            return StatusCode(403, response);
        }

        // Remaining business-state conflicts require
        // source refresh or manual resolution.
        return Conflict(response);
    }
}
