using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Accounts;
using NexaHR.Application.Interfaces.Accounts;
using NexaHR.Domain.Constants;
using System.Security.Claims;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.HRManager)]
[Route("api/v1/accounts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountsController(IAccountService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    // API-011
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] AccountQuery query)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        if (!string.IsNullOrWhiteSpace(query.Role) &&
            !AppRoles.All.Contains(query.Role))
            return BadRequest("Invalid role filter.");

        var result = await _service.GetAccountsAsync(
            actorId, query);

        if (result is null)
            return Forbid();

        return Ok(result);
    }

    // API-012
    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetAccount(
        Guid accountId)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetAccountAsync(
            actorId, accountId);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    // API-013
    [HttpPost("{accountId:guid}/status-changes")]
    public async Task<IActionResult> ChangeStatus(
        Guid accountId,
        [FromBody] ChangeAccountStatusRequest request)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.ChangeStatusAsync(
            actorId, accountId, request);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" => Forbid(),
            "not_found" => NotFound(result),
            "self_deactivation" => Conflict(result),
            "last_administrator" => Conflict(result),
            "no_change" => Conflict(result),
            "update_failed" => Conflict(result),
            _ => BadRequest(result)
        };
    }
}
