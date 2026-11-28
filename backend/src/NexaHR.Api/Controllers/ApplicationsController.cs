using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Applications;
using NexaHR.Application.Interfaces.Applications;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/applications")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _service;
    private readonly IWebHostEnvironment _environment;

    public ApplicationsController(
        IApplicationService service,
        IWebHostEnvironment environment)
    {
        _service = service;
        _environment = environment;
    }

    // API-072: Role-filtered application list.
    [HttpGet]
    [Authorize(Roles =
        AppRoles.HRManager + "," +
        AppRoles.Candidate + "," +
        AppRoles.Employee)]
    public async Task<IActionResult> GetApplications(
        [FromQuery] ApplicationQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _service.GetApplicationsAsync(
            actorUserId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result.Data);
        }

        return result.Code switch
        {
            "forbidden" => StatusCode(403, result),

            "invalid_stage" or
            "invalid_vacancy" => BadRequest(result),

            _ => StatusCode(500, new
            {
                code = "application_list_failed",
                message = "Unable to retrieve applications."
            })
        };
    }

    // API-074: Get role-filtered application details.
    [HttpGet("{applicationId}")]
    [Authorize(Roles =
        AppRoles.HRManager + "," +
        AppRoles.Candidate + "," +
        AppRoles.Employee)]
    [ProducesResponseType(
        typeof(ApplicationDetailsResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetApplicationDetails(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _service.GetApplicationDetailsAsync(
            actorUserId,
            applicationId,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result.Data);
        }

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(403, result),

            "invalid_application" =>
                BadRequest(result),

            "application_not_found" =>
                NotFound(result),

            _ => StatusCode(500, new
            {
                code = "application_details_failed",
                message = "Unable to retrieve application."
            })
        };
    }

    // API-075: Get permitted application timeline.
    [HttpGet("{applicationId}/timeline")]
    [Authorize(Roles =
        AppRoles.HRManager + "," +
        AppRoles.Candidate + "," +
        AppRoles.Employee)]
    public async Task<IActionResult> GetApplicationTimeline(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result =
            await _service.GetApplicationTimelineAsync(
                actorUserId,
                applicationId,
                cancellationToken);

        if (result.Success)
        {
            return Ok(result.Data);
        }

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(403, result),

            "invalid_application" =>
                BadRequest(result),

            "application_not_found" =>
                NotFound(result),

            _ => StatusCode(500, new
            {
                code = "application_timeline_failed",
                message = "Unable to retrieve application timeline."
            })
        };
    }

    // API-073: Submit application.
    [HttpPost]
    [Authorize(Roles = AppRoles.Candidate)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitApplicationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(503, new
            {
                Code = "submission_not_enabled",
                Message = "Application submission is not enabled."
            });
        }

        if (!Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _service.SubmitAsync(
            actorUserId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }

        return result.Code switch
        {
            "forbidden" => StatusCode(403, result),

            "vacancy_unavailable" =>
                NotFound(result),

            "duplicate_application" or
            "application_conflict" =>
                Conflict(result),

            "application_database_error" or
            "cv_integrity_failed" =>
                StatusCode(503, result),

            _ => BadRequest(result)
        };
    }

    // API-076: Application stage transitions.
    [HttpPost("{applicationId}/stage-transitions")]
    [Authorize(Roles = AppRoles.HRManager)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    [ProducesResponseType(
        typeof(ApplicationStageTransitionResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> TransitionStage(
        [FromRoute] Guid applicationId,
        [FromBody] ApplicationStageTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var actorClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(actorClaim, out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _service.TransitionStageAsync(
            actorUserId,
            applicationId,
            request,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        return result.Code switch
        {
            "forbidden" => StatusCode(403, new
            {
                code = "forbidden",
                message = "Access denied."
            }),

            "application_not_found" => NotFound(new
            {
                code = result.Code,
                message = result.Message
            }),

            "invalid_stage_transition_request" or
            "stage_transition_reason_required"
                => BadRequest(new
                {
                    code = result.Code,
                    message = result.Message
                }),

            "application_conflict" or
            "invalid_stage_transition" or
            "confirmed_cv_review_required" or
            "cv_review_revision_not_approved"
                => Conflict(new
                {
                    code = result.Code,
                    message = result.Message
                }),

            "application_database_error" or
            "cv_review_document_not_available"
                => StatusCode(503, new
                {
                    code = result.Code,
                    message = result.Message
                }),

            "application_source_invalid"
                => StatusCode(500, new
                {
                    code = "application_source_invalid",
                    message = "Application data is unavailable."
                }),

            _ => StatusCode(500, new
            {
                code = "stage_transition_failed",
                message = "Unable to complete the stage transition."
            })
        };
    }

    // API-078: Candidate-owned provider-specific AI consent.
    [HttpPut("{applicationId}/ai-consent")]
    [Authorize(Roles = AppRoles.Candidate)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    [ProducesResponseType(
        typeof(ApplicationAiConsentUpdateResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> UpdateAiConsent(
        [FromRoute] Guid applicationId,
        [FromBody] ApplicationAiConsentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _service.UpdateAiConsentAsync(
            actorUserId,
            applicationId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result.Data);
        }

        return result.Code switch
        {
            "forbidden" => StatusCode(403, new
            {
                code = "forbidden",
                message = "Access denied."
            }),

            "invalid_ai_consent_request" or
            "ai_consent_policy_invalid" =>
                BadRequest(result),

            "application_not_found" or
            "ai_consent_not_found" =>
                NotFound(result),

            "application_conflict" or
            "ai_consent_policy_conflict" or
            "ai_consent_stage_conflict" or
            "ai_consent_text_conflict" =>
                Conflict(result),

            "application_database_error" =>
                StatusCode(503, new
                {
                    code = "ai_consent_unavailable",
                    message = "Unable to update AI consent."
                }),

            "application_source_invalid" =>
                StatusCode(500, new
                {
                    code = "ai_consent_data_unavailable",
                    message = "Unable to verify consent information."
                }),

            _ => StatusCode(500, new
            {
                code = "ai_consent_update_failed",
                message = "Unable to update AI consent."
            })
        };
    }
}
