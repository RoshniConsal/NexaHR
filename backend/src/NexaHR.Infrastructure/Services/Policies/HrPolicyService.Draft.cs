using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.Policies;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Policies;

public sealed partial class HrPolicyService
{
    private void StagePolicyAudit(
        Guid actorUserId,
        string actorRole,
        string action,
        string entityType,
        Guid entityId)
    {
        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Module = "Policies",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Outcome = "success"
        });
    }

    // API-045: Create policy with initial draft.
    public async Task<PolicyOperationResult<PolicyDetailsResponse>>
        CreatePolicyAsync(
            Guid actorUserId,
            CreatePolicyRequest request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanManage(role))
        {
            return Fail<PolicyDetailsResponse>(
                "Forbidden",
                "Only HR managers can create policies.");
        }

        var validationError = ValidateDraft(request);

        if (validationError is not null)
        {
            return Fail<PolicyDetailsResponse>(
                "ValidationError",
                validationError);
        }

        var policy = new HrPolicy
        {
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        ApplyDraft(policy, request);

        _db.HrPolicies.Add(policy);

        StagePolicyAudit(
            actorUserId,
            role!,
            "PolicyCreated",
            "HrPolicy",
            policy.Id);

        // EF Core commits the policy and audit event together.
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(
            new PolicyDetailsResponse(
                policy.Id,
                null,
                null,
                Array.Empty<PolicyVersionSummary>(),
                new PolicyDraftResponse(
                    policy.DraftTitle!,
                    policy.DraftContent!,
                    policy.DraftRequiresAcknowledgement,
                    policy.DraftVisibleToEmployees,
                    policy.DraftVisibleToHr,
                    policy.DraftVisibleToCeo),
                Convert.ToBase64String(policy.RowVersion)),
            "Policy draft created.");
    }

    // API-047: Create or update an unpublished draft.
    public async Task<PolicyOperationResult<PolicyDetailsResponse>>
        UpdateDraftAsync(
            Guid actorUserId,
            Guid policyId,
            UpdatePolicyDraftRequest request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanManage(role))
        {
            return Fail<PolicyDetailsResponse>(
                "Forbidden",
                "Only HR managers can update policy drafts.");
        }

        var validationError = ValidateDraft(request);

        if (validationError is not null)
        {
            return Fail<PolicyDetailsResponse>(
                "ValidationError",
                validationError);
        }

        if (!TryParseRowVersion(
                request.RowVersion,
                out var rowVersion))
        {
            return Fail<PolicyDetailsResponse>(
                "ValidationError",
                "Invalid row version.");
        }

        var policy = await _db.HrPolicies
            .SingleOrDefaultAsync(
                x => x.Id == policyId,
                cancellationToken);

        if (policy is null)
        {
            return Fail<PolicyDetailsResponse>(
                "NotFound",
                "Policy was not found.");
        }

        if (!policy.RowVersion.SequenceEqual(rowVersion))
        {
            return Fail<PolicyDetailsResponse>(
                "Conflict",
                "Policy has changed. Reload before updating.");
        }

        var unchanged =
            policy.HasDraft &&
            policy.DraftTitle == request.Title.Trim() &&
            policy.DraftContent == request.Content.Trim() &&
            policy.DraftRequiresAcknowledgement ==
                request.RequiresAcknowledgement &&
            policy.DraftVisibleToEmployees ==
                request.VisibleToEmployees &&
            policy.DraftVisibleToHr ==
                request.VisibleToHr &&
            policy.DraftVisibleToCeo ==
                request.VisibleToCeo;

        if (unchanged)
        {
            return await GetPolicyAsync(
                actorUserId,
                policyId,
                cancellationToken);
        }

        _db.Entry(policy)
            .Property(x => x.RowVersion)
            .OriginalValue = rowVersion;

        ApplyDraft(policy, request);

        policy.UpdatedByUserId = actorUserId;
        policy.UpdatedAtUtc = DateTime.UtcNow;

        StagePolicyAudit(
            actorUserId,
            role!,
            "PolicyDraftUpdated",
            "HrPolicy",
            policy.Id);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();

            return Fail<PolicyDetailsResponse>(
                "Conflict",
                "Policy changed during update. Reload and retry.");
        }

        return await GetPolicyAsync(
            actorUserId,
            policyId,
            cancellationToken);
    }
}
