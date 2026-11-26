using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Vacancies;
using NexaHR.Application.Interfaces.Vacancies;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/vacancies")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VacanciesController : ControllerBase
{
    private readonly IVacancyService _service;

    public VacanciesController(IVacancyService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    // API-063: List vacancies
    [HttpGet]
    [Authorize(Roles = AppRoles.HRManager + "," + AppRoles.CEO)]
    [ProducesResponseType(typeof(VacancyListResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetVacancies(
        [FromQuery] VacancyQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        string[] validStatuses =
        [
            VacancyStatuses.Draft,
            VacancyStatuses.PendingApproval,
            VacancyStatuses.Approved,
            VacancyStatuses.Rejected,
            VacancyStatuses.Published,
            VacancyStatuses.Paused,
            VacancyStatuses.Closed
        ];

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = validStatuses.FirstOrDefault(s =>
                string.Equals(
                    s,
                    query.Status.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (status is null)
            {
                return BadRequest(new
                {
                    success = false,
                    code = "validation_error",
                    message = "Invalid vacancy status."
                });
            }

            query.Status = status;
        }

        if (query.DepartmentId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                code = "validation_error",
                message = "Invalid department ID."
            });
        }

        var result = await _service.GetVacanciesAsync(
            actorId,
            query,
            cancellationToken);

        if (result is null)
            return Forbid();

        return Ok(result);
    }

    // API-064: Create vacancy draft
    // Requirement: VAC-01
    [HttpPost]
    [Authorize(Roles = AppRoles.HRManager)]
    [ProducesResponseType(typeof(VacancyOperationResponse), 201)]
    [ProducesResponseType(typeof(VacancyOperationResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(VacancyOperationResponse), 404)]
    [ProducesResponseType(typeof(VacancyOperationResponse), 409)]
    public async Task<IActionResult> CreateVacancy(
        [FromBody] CreateVacancyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.CreateAsync(
            actorId,
            request,
            cancellationToken);

        if (result.Success)
            return StatusCode(201, result);

        return result.Code switch
        {
            "forbidden" => Forbid(),
            "not_found" => NotFound(result),
            "archived" => Conflict(result),
            "duplicate_record" => Conflict(result),
            "concurrency_conflict" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    // API-065: Get vacancy details
    [HttpGet("{vacancyId:guid}")]
    [Authorize(Roles = AppRoles.HRManager + "," + AppRoles.CEO)]
    [ProducesResponseType(typeof(VacancyDetailsResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVacancyDetails(
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
        {
            return Unauthorized();
        }

        if (vacancyId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                code = "validation_error",
                message = "Invalid vacancy ID."
            });
        }

        VacancyDetailsResponse? result;

        try
        {
            result = await _service.GetVacancyDetailsAsync(
                actorId,
                vacancyId,
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (result is null)
        {
            return NotFound(new
            {
                success = false,
                code = "not_found",
                message = "Vacancy not found."
            });
        }

        return Ok(result);
    }

    // API-066: Update vacancy and manage revisions
    [HttpPatch("{vacancyId:guid}")]
    [Authorize(Roles = AppRoles.HRManager)]
    [ProducesResponseType(
        typeof(UpdateVacancyOperationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateVacancy(
        Guid vacancyId,
        [FromBody] UpdateVacancyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
            return Unauthorized();

        var result = await _service.UpdateAsync(
            actorUserId,
            vacancyId,
            request,
            cancellationToken);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(result),

            "invalid_state" or
            "archived" or
            "concurrency_conflict" or
            "capacity_validation_pending"
                => Conflict(result),

            _ => BadRequest(result)
        };
    }

    // API-067: Retrieve vacancy revision and decision history.
    [HttpGet("{vacancyId:guid}/revisions")]
    [Authorize(Roles = AppRoles.HRManager + "," + AppRoles.CEO)]
    [ProducesResponseType(
        typeof(VacancyRevisionHistoryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRevisionHistory(
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
        {
            return Unauthorized();
        }

        if (vacancyId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                code = "validation_error",
                message = "A valid vacancy ID is required."
            });
        }

        try
        {
            var result = await _service.GetRevisionHistoryAsync(
                actorUserId,
                vacancyId,
                cancellationToken);

            if (result is null)
            {
                return NotFound(new
                {
                    success = false,
                    code = "not_found",
                    message = "Vacancy not found."
                });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }


    // API-068: Submit vacancy for CEO approval
    [HttpPost("{vacancyId:guid}/submissions")]
    [Authorize(Roles = AppRoles.HRManager)]
    [ProducesResponseType(
        typeof(SubmitVacancyOperationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid vacancyId,
        [FromBody] SubmitVacancyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
            return Unauthorized();

        var result = await _service.SubmitAsync(
            actorUserId,
            vacancyId,
            request,
            cancellationToken);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(result),

            "invalid_state" or
            "archived" or
            "concurrency_conflict" => Conflict(result),

            _ => BadRequest(result)
        };
    }

    // API-069: CEO approval or rejection of exact revision
    // Requirement: VAC-02
    [HttpPost("{vacancyId:guid}/decisions")]
    [Authorize(Roles = AppRoles.CEO)]
    [ProducesResponseType(
        typeof(DecideVacancyOperationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DecideVacancy(
        Guid vacancyId,
        [FromBody] DecideVacancyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _service.DecideAsync(
            actorUserId,
            vacancyId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(result),

            "invalid_state" or
            "concurrency_conflict" => Conflict(result),

            _ => BadRequest(result)
        };
    }

    // API-070: Publish the exact CEO-approved vacancy revision
    // Requirement: VAC-03
    [HttpPost("{vacancyId:guid}/publications")]
    [Authorize(Roles = AppRoles.HRManager)]
    [ProducesResponseType(
        typeof(PublishVacancyOperationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishVacancy(
        Guid vacancyId,
        [FromBody] PublishVacancyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _service.PublishAsync(
            actorUserId,
            vacancyId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(result),

            "invalid_state" or
            "expired" or
            "archived" or
            "concurrency_conflict" => Conflict(result),

            _ => BadRequest(result)
        };
    }

    // API-071: Close a published vacancy
    // Requirements: VAC-05, VAC-06
    [HttpPost("{vacancyId:guid}/closures")]
    [Authorize(Roles = AppRoles.HRManager)]
    [ProducesResponseType(
        typeof(CloseVacancyOperationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseVacancy(
        Guid vacancyId,
        [FromBody] CloseVacancyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _service.CloseAsync(
            actorUserId,
            vacancyId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(result),

            "invalid_state" or
            "concurrency_conflict" => Conflict(result),

            _ => BadRequest(result)
        };
    }
}
