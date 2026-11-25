using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/audit-events")]
[Authorize(Roles = AppRoles.HRManager + "," + AppRoles.CEO)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuditEventsController : ControllerBase
{
    private readonly IAuditService _service;

    public AuditEventsController(IAuditService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    private IActionResult HandleResult<T>(
        AuditOperationResponse<T> result)
        where T : class
    {
        if (result.Success && result.Data is not null)
            return Ok(result.Data);

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(new
            {
                result.Code,
                result.Message
            }),

            "export_limit_exceeded" => BadRequest(new
            {
                result.Code,
                result.Message
            }),

            _ => BadRequest(new
            {
                result.Code,
                result.Message
            })
        };
    }

    // API-152: Filtered audit events and CSV export
    [HttpGet]
    public async Task<IActionResult> GetAuditEvents(
        [FromQuery] AuditEventQuery query)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        if (query.Format == "csv")
        {
            var export = await _service
                .ExportAuditEventsAsync(actorId, query);

            if (!export.Success || export.Data is null)
                return HandleResult(export);

            return File(
                export.Data.Content,
                export.Data.ContentType,
                export.Data.FileName);
        }

        var result = await _service
            .GetAuditEventsAsync(actorId, query);

        return HandleResult(result);
    }

    // API-153: Authorized event detail
    [HttpGet("{eventId:guid}")]
    public async Task<IActionResult> GetAuditEvent(
        Guid eventId)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service
            .GetAuditEventAsync(actorId, eventId);

        return HandleResult(result);
    }
}
