using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NexaHR.Application.DTOs.Employees;
using NexaHR.Application.Interfaces.Employees;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;

    private const string DirectoryRoles =
        AppRoles.HRManager + "," +
        AppRoles.CEO + "," +
        AppRoles.Employee;

    private const string HistoryRoles =
        AppRoles.HRManager + "," +
        AppRoles.Employee;

    public EmployeesController(IEmployeeService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    private IActionResult ToResponse<T>(
        EmployeeOperationResult<T> result)
    {
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "Forbidden" => StatusCode(403, result),
            "NotFound" => NotFound(result),
            "Conflict" => Conflict(result),
            "ValidationError" => BadRequest(result),
            _ => StatusCode(500, new
            {
                success = false,
                code = "InternalError",
                message = "The request could not be completed."
            })
        };
    }

    // API-051: Employee directory
    [HttpGet]
    [Authorize(Roles = DirectoryRoles)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] EmployeeDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetEmployeesAsync(
            actorId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    // API-052: Employee details
    [HttpGet("{employeeId:guid}")]
    [Authorize(Roles = DirectoryRoles)]
    public async Task<IActionResult> GetEmployee(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetEmployeeAsync(
            actorId,
            employeeId,
            cancellationToken);

        return ToResponse(result);
    }

    // API-053: Update employee
    [HttpPatch("{employeeId:guid}")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> UpdateEmployee(
        Guid employeeId,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.UpdateEmployeeAsync(
            actorId,
            employeeId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    // API-054: Request work email verification
    [HttpPost("{employeeId:guid}/work-email-verifications")]
    [EnableRateLimiting("auth-sensitive")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> RequestVerification(
        Guid employeeId,
        [FromBody] RequestWorkEmailVerification request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _service.RequestWorkEmailVerificationAsync(
                actorId,
                employeeId,
                request,
                cancellationToken);

        return ToResponse(result);
    }

    // API-055: Confirm work email verification
    [HttpPost("work-email-verifications/confirm")]
    [EnableRateLimiting("auth-sensitive")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmVerification(
        [FromBody] ConfirmWorkEmailVerification request,
        CancellationToken cancellationToken)
    {
        var result =
            await _service.ConfirmWorkEmailVerificationAsync(
                request,
                cancellationToken);

        return ToResponse(result);
    }

    // API-056: Employment history
    [HttpGet("{employeeId:guid}/employment-history")]
    [Authorize(Roles = HistoryRoles)]
    public async Task<IActionResult> GetEmploymentHistory(
        Guid employeeId,
        [FromQuery] EmploymentHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result =
            await _service.GetEmploymentHistoryAsync(
                actorId,
                employeeId,
                request,
                cancellationToken);

        return ToResponse(result);
    }
}
