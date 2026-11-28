using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaHR.Application.DTOs.Policies;
using NexaHR.Application.Interfaces.Policies;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/policies")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class PoliciesController : ControllerBase
{
    private readonly IHrPolicyService _service;

    private const string ReadRoles =
        AppRoles.HRManager + "," +
        AppRoles.CEO + "," +
        AppRoles.Employee;

    private const string AcknowledgementRoles =
        AppRoles.HRManager + "," +
        AppRoles.Employee;

    public PoliciesController(IHrPolicyService service)
    {
        _service = service;
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId) &&
            actorId != Guid.Empty;
    }

    private IActionResult ToResponse<T>(
        PolicyOperationResult<T> result)
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

    // API-044: List visible policies.
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    public async Task<IActionResult> GetPolicies(
        [FromQuery] PolicyListQuery request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetPoliciesAsync(
            actorId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    // API-045: Create policy with initial draft.
    [HttpPost]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] CreatePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.CreatePolicyAsync(
            actorId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    // API-046: Get policy details and version history.
    [HttpGet("{policyId:guid}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<IActionResult> GetPolicy(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetPolicyAsync(
            actorId,
            policyId,
            cancellationToken);

        return ToResponse(result);
    }

    // API-047: Create or update unpublished draft.
    [HttpPut("{policyId:guid}/draft")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> UpdateDraft(
        Guid policyId,
        [FromBody] UpdatePolicyDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.UpdateDraftAsync(
            actorId,
            policyId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    // API-048: Publish immutable policy version.
    [HttpPost("{policyId:guid}/publications")]
    [Authorize(Roles = AppRoles.HRManager)]
    public async Task<IActionResult> PublishPolicy(
        Guid policyId,
        [FromBody] PublishPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.PublishPolicyAsync(
            actorId,
            policyId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    // API-049: Get exact published version.
    [HttpGet("{policyId:guid}/versions/{version:int}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<IActionResult> GetPolicyVersion(
        Guid policyId,
        int version,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.GetPolicyVersionAsync(
            actorId,
            policyId,
            version,
            cancellationToken);

        return ToResponse(result);
    }

    // API-050: Acknowledge exact published version.
    [HttpPost(
        "{policyId:guid}/versions/{version:int}/acknowledgements")]
    [Authorize(Roles = AcknowledgementRoles)]
    public async Task<IActionResult> AcknowledgePolicy(
        Guid policyId,
        int version,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var result = await _service.AcknowledgePolicyAsync(
            actorId,
            policyId,
            version,
            cancellationToken);

        return ToResponse(result);
    }
}
