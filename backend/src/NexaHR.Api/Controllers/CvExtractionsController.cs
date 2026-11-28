using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.CvExtractions;
using NexaHR.Application.Interfaces.CvExtractions;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.HRManager)]
[Route("api/v1")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class CvExtractionsController : ControllerBase
{
    private readonly ICvExtractionService _service;

    public CvExtractionsController(
        ICvExtractionService service)
    {
        _service = service;
    }

    private bool TryGetActor(out Guid actorUserId)
    {
        return Guid.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier),
            out actorUserId) &&
            actorUserId != Guid.Empty;
    }

    // API-084: Queue a new CV extraction run.
    [HttpPost("applications/{applicationId}/extractions")]
    [ProducesResponseType(
        typeof(CvExtractionResponse),
        StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Start(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId))
            return Unauthorized();

        var result = await _service.StartAsync(
            actorUserId,
            applicationId,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return AcceptedAtAction(
                nameof(GetResult),
                new { runId = result.Data.Id },
                result.Data);
        }

        return MapFailure(result);
    }

    // API-085: Retrieve extraction run history.
    [HttpGet("applications/{applicationId}/extractions")]
    [ProducesResponseType(
        typeof(IReadOnlyList<CvExtractionResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId))
            return Unauthorized();

        var result = await _service.GetHistoryAsync(
            actorUserId,
            applicationId,
            cancellationToken);

        if (result.Success && result.Data is not null)
            return Ok(result.Data);

        return MapFailure(result);
    }

    // API-086: Retrieve a specific extraction result.
    [HttpGet("extractions/{runId}")]
    [ProducesResponseType(
        typeof(CvExtractionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult(
        [FromRoute] Guid runId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId))
            return Unauthorized();

        var result = await _service.GetResultAsync(
            actorUserId,
            runId,
            cancellationToken);

        if (result.Success && result.Data is not null)
            return Ok(result.Data);

        return MapFailure(result);
    }

    private IActionResult MapFailure<T>(
        CvExtractionOperationResponse<T> result)
        where T : class
    {
        var response = new
        {
            code = result.Code,
            message = result.Message
        };

        return result.Code switch
        {
            "forbidden" =>
                StatusCode(403, response),

            "invalid_application" =>
                BadRequest(response),

            "application_not_found" or
            "extraction_not_found" =>
                NotFound(response),

            "extraction_in_progress" or
            "extraction_write_conflict" or
            "revision_not_approved" =>
                Conflict(response),

            "document_not_available" or
            "extraction_database_error" =>
                StatusCode(503, response),

            _ => StatusCode(500, new
            {
                code = "extraction_unavailable",
                message = "CV extraction is unavailable."
            })
        };
    }
}
