using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Jobs;
using NexaHR.Application.Interfaces.Vacancies;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/jobs")]
[AllowAnonymous]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class JobsController : ControllerBase
{
    private readonly IVacancyService _vacancies;

    public JobsController(IVacancyService vacancies)
    {
        _vacancies = vacancies;
    }

    // API-061: Public listing of published open jobs
    [HttpGet]
    [ProducesResponseType(
        typeof(PublicJobListResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicJobListResponse>>
        GetJobs(
            [FromQuery] PublicJobQuery request,
            CancellationToken cancellationToken)
    {
        var result = await _vacancies.GetPublicJobsAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    // API-062: Sanitised published open job details
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(
        typeof(PublicJobDetailsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicJobDetailsResponse>>
        GetJobDetails(
            Guid jobId,
            CancellationToken cancellationToken)
    {
        var result =
            await _vacancies.GetPublicJobDetailsAsync(
                jobId,
                cancellationToken);

        if (result is null)
        {
            return NotFound(new
            {
                code = "job_not_found",
                message = "Job not found."
            });
        }

        return Ok(result);
    }
}
