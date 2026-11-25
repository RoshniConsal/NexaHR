using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.Interfaces.Dashboard;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    // API-148: Role-specific dashboard
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetDashboardAsync(actorId);

        return result is null ? Forbid() : Ok(result);
    }

    // API-149: Authorized pending actions
    [HttpGet("pending-actions")]
    public async Task<IActionResult> GetPendingActions()
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetPendingActionsAsync(actorId);

        return result is null ? Forbid() : Ok(result);
    }
}
