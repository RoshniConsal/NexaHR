using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Notifications;
using NexaHR.Application.Interfaces.Notifications;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
    }

    // API-144: List own notifications
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] NotificationQuery query)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetNotificationsAsync(
            actorId, query);

        return result is null ? Forbid() : Ok(result);
    }

    // API-145: Own notification details
    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> GetNotification(
        Guid notificationId)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetNotificationAsync(
            actorId, notificationId);

        return HandleResult(result);
    }

    // API-146: Mark own notification read
    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.MarkAsReadAsync(
            actorId, notificationId);

        return HandleResult(result);
    }

    // API-147: Mark all own notifications read
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.MarkAllAsReadAsync(actorId);

        return HandleResult(result);
    }

    private IActionResult HandleResult(
        NotificationOperationResponse result)
    {
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" => Forbid(),
            "not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }
}
