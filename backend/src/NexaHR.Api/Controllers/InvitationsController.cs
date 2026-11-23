using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NexaHR.Application.DTOs.Invitations;
using NexaHR.Application.Interfaces.Invitations;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Route("api/v1/invitations")]
[Authorize(Roles = AppRoles.HRManager)]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class InvitationsController : ControllerBase
{
    private readonly IInvitationService _service;

    public InvitationsController(
        IInvitationService service)
    {
        _service = service;
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    // API-014
    [HttpGet]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] InvitationQuery query)
    {
        var actorId = GetUserId();

        if (!actorId.HasValue)
            return Unauthorized();

        if (!string.IsNullOrWhiteSpace(query.Role) &&
            !AppRoles.All.Contains(query.Role))
        {
            return BadRequest(new
            {
                message = "Invalid role filter."
            });
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            query.Status is not
                ("Pending" or "Accepted" or
                 "Expired" or "Revoked"))
        {
            return BadRequest(new
            {
                message = "Invalid invitation status."
            });
        }

        var result = await _service.GetInvitationsAsync(
            actorId.Value, query);

        return result is null
            ? Forbid()
            : Ok(result);
    }

    // API-015
    [HttpPost]
    public async Task<IActionResult> Issue(
        IssueInvitationRequest request)
    {
        var actorId = GetUserId();

        if (!actorId.HasValue)
            return Unauthorized();

        var result = await _service.IssueAsync(
            actorId.Value, request);

        return result.Success
            ? StatusCode(StatusCodes.Status201Created, result)
            : OperationResult(result);
    }

    // API-016
    [HttpPost("{invitationId:guid}/reissues")]
    public async Task<IActionResult> Reissue(
        Guid invitationId,
        ReissueInvitationRequest request)
    {
        var actorId = GetUserId();

        if (!actorId.HasValue)
            return Unauthorized();

        var result = await _service.ReissueAsync(
            actorId.Value,
            invitationId,
            request);

        return result.Success
            ? StatusCode(StatusCodes.Status201Created, result)
            : OperationResult(result);
    }

    // API-017
    [HttpPost("{invitationId:guid}/revocations")]
    public async Task<IActionResult> Revoke(
        Guid invitationId,
        RevokeInvitationRequest request)
    {
        var actorId = GetUserId();

        if (!actorId.HasValue)
            return Unauthorized();

        var result = await _service.RevokeAsync(
            actorId.Value,
            invitationId,
            request);

        return OperationResult(result);
    }

    // API-018
    [HttpPost("preview")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Preview(
        InvitationTokenRequest request)
    {
        var result = await _service.PreviewAsync(request);

        return result.IsValid
            ? Ok(result)
            : BadRequest(result);
    }

    // API-019
    [HttpPost("accept")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Accept(
        AcceptInvitationRequest request)
    {
        var userId = GetUserId();

        var authenticated =
            User.Identity?.IsAuthenticated == true;

        if (authenticated && !userId.HasValue)
            return Unauthorized();

        // Do not silently treat invalid bearer tokens
        // as anonymous account-creation requests.
        if (!authenticated &&
            Request.Headers.ContainsKey("Authorization"))
        {
            return Unauthorized();
        }

        var result = await _service.AcceptAsync(
            authenticated ? userId : null,
            request);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "identity_mismatch" =>
                StatusCode(StatusCodes.Status403Forbidden,
                    result),

            "sign_in_required" =>
                Unauthorized(result),

            "role_conflict" =>
                Conflict(result),

            "role_assignment_failed" =>
                Conflict(result),

            _ => BadRequest(result)
        };
    }

    private IActionResult OperationResult(
        InvitationOperationResponse result)
    {
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "forbidden" => Forbid(),

            "not_found" => NotFound(result),

            "existing_invitation" => Conflict(result),

            "account_conflict" => Conflict(result),

            "already_accepted" => Conflict(result),

            _ => BadRequest(result)
        };
    }
}
