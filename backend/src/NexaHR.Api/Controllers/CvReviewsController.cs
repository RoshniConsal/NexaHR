using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.Interfaces.CvReviews;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.HRManager)]
[Route("api/v1/applications/{applicationId}/cv-review")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class CvReviewsController : ControllerBase
{
    private readonly ICvReviewService _service;

    public CvReviewsController(
        ICvReviewService service)
    {
        _service = service;
    }

    // API-079: Retrieve the HR-only CV review workspace.
    [HttpGet]
    [ProducesResponseType(
        typeof(CvReviewWorkspaceResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetWorkspace(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier),
                out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await _service.GetWorkspaceAsync(
            actorUserId,
            applicationId,
            cancellationToken);

        if (result.Success && result.Data is not null)
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

            "cv_review_revision_not_approved" =>
                Conflict(result),

            "cv_review_document_not_available" or
            "cv_review_schema_not_enabled" or
            "cv_review_database_error" =>
                StatusCode(503, result),

            "cv_review_json_invalid" or
            "cv_review_source_invalid" =>
                StatusCode(500, new
                {
                    code = "cv_review_data_unavailable",
                    message =
                        "CV review data is unavailable."
                }),

            _ => StatusCode(500, new
            {
                code = "cv_review_workspace_failed",
                message =
                    "Unable to retrieve CV review workspace."
            })
        };
    }

    // API-080 | HR Manager CV review draft.
    [HttpPut("draft")]
    [ProducesResponseType(
        typeof(CvReviewDraftResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SaveDraft(
        [FromRoute] Guid applicationId,
        [FromBody] CvReviewDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier),
                out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                code = "unauthorized",
                message = "Authentication is required."
            });
        }

        var result = await _service.SaveDraftAsync(
            actorUserId,
            applicationId,
            request,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        var response = new
        {
            code = result.Code,
            message = result.Message,
            errors = result.Errors
        };

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        code = "forbidden",
                        message = "Access denied."
                    }),

            "application_not_found" =>
                NotFound(response),

            "invalid_application" or
            "invalid_cv_review_request" or
            "invalid_cv_review_values" or
            "invalid_cv_review_duplicate_resolution" =>
                BadRequest(response),

            "cv_review_conflict" or
            "cv_review_stage_conflict" or
            "cv_review_revision_not_approved" or
            "cv_review_extraction_pending" or
            "cv_review_no_changes" or
            "cv_review_duplicate_resolution_stale" =>
                Conflict(response),

            "cv_review_document_not_available" or
            "cv_review_database_error" or
            "database_error" =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "cv_review_unavailable",
                        message =
                            "CV review is temporarily unavailable."
                    }),

            "cv_review_json_invalid" or
            "cv_review_source_invalid" =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_data_unavailable",
                        message =
                            "CV review data is unavailable."
                    }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_draft_failed",
                        message =
                            "The CV review draft could not be saved."
                    })
        };
    }


    // API-081: Confirm the exact latest saved CV review.
    [HttpPost("confirmations")]
    [ProducesResponseType(
        typeof(CvReviewConfirmationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfirmReview(
        [FromRoute] Guid applicationId,
        [FromBody] CvReviewConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                code = "unauthorized",
                message = "Authentication is required."
            });
        }

        var result = await _service.ConfirmReviewAsync(
            actorUserId,
            applicationId,
            request,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        var error = new
        {
            code = result.Code,
            message = result.Message,
            errors = result.Errors
        };

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        code = "forbidden",
                        message = "Access denied."
                    }),

            "application_not_found" =>
                NotFound(error),

            "invalid_cv_review_request" or
            "invalid_cv_review_values" or
            "invalid_cv_review_duplicate_resolution" =>
                BadRequest(error),

            "cv_review_conflict" or
            "cv_review_stage_conflict" or
            "cv_review_revision_not_approved" or
            "cv_review_extraction_pending" or
            "cv_review_draft_required" or
            "cv_review_already_confirmed" =>
                Conflict(error),

            "cv_review_document_not_available" or
            "cv_review_database_error" =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "cv_review_unavailable",
                        message =
                            "CV review is temporarily unavailable."
                    }),

            "cv_review_json_invalid" or
            "cv_review_source_invalid" =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_data_unavailable",
                        message =
                            "Unable to process CV review data."
                    }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_confirmation_failed",
                        message =
                            "Unable to confirm CV review."
                    })
        };
    }

    // API-082: Retrieve immutable HR-only CV review history.
    [HttpGet("history")]
    [ProducesResponseType(
        typeof(CvReviewHistoryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReviewHistory(
        [FromRoute] Guid applicationId,
        [FromQuery] CvReviewHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value,
                out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                code = "unauthorized",
                message = "Authentication is required."
            });
        }

        var result = await _service.GetReviewHistoryAsync(
            actorUserId,
            applicationId,
            query,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        var error = new
        {
            code = result.Code,
            message = result.Message
        };

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        code = "forbidden",
                        message = "Access denied."
                    }),

            "invalid_application" or
            "invalid_cv_review_history_query" =>
                BadRequest(error),

            "application_not_found" =>
                NotFound(error),

            "cv_review_revision_not_approved" =>
                Conflict(error),

            "cv_review_document_not_available" or
            "cv_review_database_error" =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "cv_review_history_unavailable",
                        message =
                            "CV review history is temporarily unavailable."
                    }),

            "cv_review_json_invalid" or
            "cv_review_source_invalid" =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_data_unavailable",
                        message =
                            "CV review history is unavailable."
                    }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_history_failed",
                        message =
                            "Unable to retrieve CV review history."
                    })
        };
    }

    // API-083: Start explicit re-review against
    // a selected CEO-approved vacancy criteria revision.
    [HttpPost("revalidations")]
    [ProducesResponseType(
        typeof(CvReviewRevalidationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RevalidateReview(
        [FromRoute] Guid applicationId,
        [FromBody] CvReviewRevalidationRequest request,
        CancellationToken cancellationToken)
    {
        var actorClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(actorClaim, out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                code = "unauthorized",
                message = "Authentication is required."
            });
        }

        var result = await _service.RevalidateReviewAsync(
            actorUserId,
            applicationId,
            request,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return Ok(result.Data);
        }

        var error = new
        {
            code = result.Code,
            message = result.Message,
            errors = result.Errors
        };

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        code = "forbidden",
                        message = "Access is denied."
                    }),

            "application_not_found" =>
                NotFound(error),

            "invalid_application" or
            "invalid_cv_review_request" or
            "invalid_cv_review_values" or
            "invalid_cv_review_duplicate_resolution" or
            "cv_review_target_revision_invalid" =>
                BadRequest(error),

            "cv_review_conflict" or
            "cv_review_stage_conflict" or
            "cv_review_draft_required" or
            "cv_review_revision_unchanged" or
            "cv_review_revision_not_approved" or
            "cv_review_extraction_pending" =>
                Conflict(error),

            "cv_review_document_not_available" or
            "cv_review_database_error" =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "cv_review_unavailable",
                        message = "CV review is temporarily unavailable. Refresh before retrying."
                    }),

            "cv_review_json_invalid" or
            "cv_review_source_invalid" =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        code = "cv_review_data_unavailable",
                        message = "CV review data is unavailable."
                    }),

            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "cv_review_revalidation_failed",
                    message = "Unable to complete CV review revalidation."
                })
        };
    }
}
