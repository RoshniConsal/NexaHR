using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Vacancies;
using NexaHR.Application.Interfaces.Vacancies;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/vacancies")]
[Authorize(Roles = AppRoles.HRManager)]
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

    // API-064: Create vacancy draft
    // Requirement: VAC-01
    [HttpPost]
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
}
