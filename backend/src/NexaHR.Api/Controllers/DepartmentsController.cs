using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Departments;
using NexaHR.Application.Interfaces.Departments;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize(Roles = AppRoles.HRManager)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _service;

    public DepartmentsController(IDepartmentService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    // API-023: List departments
    [HttpGet]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] DepartmentQuery query)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetDepartmentsAsync(
            actorId, query);

        return result is null ? Forbid() : Ok(result);
    }

    // API-024: Create department
    [HttpPost]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateDepartmentRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.CreateAsync(
            actorId, request);

        return HandleResult(result, 201);
    }

    // API-025: Update department
    [HttpPatch("{departmentId:guid}")]
    public async Task<IActionResult> UpdateDepartment(
        Guid departmentId,
        [FromBody] UpdateDepartmentRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.UpdateAsync(
            actorId, departmentId, request);

        return HandleResult(result);
    }

    // API-026: Archive department
    [HttpPost("{departmentId:guid}/archive")]
    public async Task<IActionResult> ArchiveDepartment(
        Guid departmentId,
        [FromBody] ArchiveDepartmentRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.ArchiveAsync(
            actorId, departmentId, request);

        return HandleResult(result);
    }

    private IActionResult HandleResult(
        DepartmentOperationResponse result,
        int successStatus = 200)
    {
        if (result.Success)
            return StatusCode(successStatus, result);

        return result.Code switch
        {
            "forbidden" => Forbid(),
            "not_found" => NotFound(result),
            "duplicate_department" => Conflict(result),
            "concurrency_conflict" => Conflict(result),
            "archived" => Conflict(result),
            "already_archived" => Conflict(result),
            "reference_validation_pending" => Conflict(result),
            _ => BadRequest(result)
        };
    }
}
