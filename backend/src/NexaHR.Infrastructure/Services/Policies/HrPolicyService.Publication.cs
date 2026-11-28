using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Policies;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Policies;

public sealed partial class HrPolicyService
{
    // API-048: Publish the exact current draft.
    public async Task<PolicyOperationResult<PolicyVersionResponse>>
        PublishPolicyAsync(
            Guid actorUserId,
            Guid policyId,
            PublishPolicyRequest request,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanManage(role))
        {
            return Fail<PolicyVersionResponse>(
                "Forbidden",
                "Only HR managers can publish policies.");
        }

        if (!TryParseRowVersion(
                request.RowVersion,
                out var rowVersion))
        {
            return Fail<PolicyVersionResponse>(
                "ValidationError",
                "Invalid row version.");
        }

        var policy = await _db.HrPolicies
            .SingleOrDefaultAsync(
                x => x.Id == policyId,
                cancellationToken);

        if (policy is null)
        {
            return Fail<PolicyVersionResponse>(
                "NotFound",
                "Policy was not found.");
        }

        if (!policy.RowVersion.SequenceEqual(rowVersion))
        {
            return Fail<PolicyVersionResponse>(
                "Conflict",
                "Policy has changed. Reload before publishing.");
        }

        if (!policy.HasDraft)
        {
            return Fail<PolicyVersionResponse>(
                "Conflict",
                "There is no unpublished draft.");
        }

        var draft = new CreatePolicyRequest
        {
            Title = policy.DraftTitle ?? string.Empty,

            Content = policy.DraftContent ?? string.Empty,

            RequiresAcknowledgement =
                policy.DraftRequiresAcknowledgement,

            VisibleToEmployees =
                policy.DraftVisibleToEmployees,

            VisibleToHr =
                policy.DraftVisibleToHr,

            VisibleToCeo =
                policy.DraftVisibleToCeo
        };

        var validationError = ValidateDraft(draft);

        if (validationError is not null)
        {
            return Fail<PolicyVersionResponse>(
                "ValidationError",
                validationError);
        }

        var latestVersion = await _db.HrPolicyVersions
            .Where(x => x.PolicyId == policyId)
            .MaxAsync(
                x => (int?)x.Version,
                cancellationToken);

        if (latestVersion != policy.CurrentPublishedVersion)
        {
            return Fail<PolicyVersionResponse>(
                "Conflict",
                "Policy publication history has changed. Reload and retry.");
        }

        if (latestVersion == int.MaxValue)
        {
            return Fail<PolicyVersionResponse>(
                "Conflict",
                "Policy version limit reached.");
        }

        var nextVersion = (latestVersion ?? 0) + 1;

        var now = DateTime.UtcNow;

        var publishedVersion = new HrPolicyVersion
        {
            PolicyId = policy.Id,

            Version = nextVersion,

            Title = draft.Title.Trim(),

            Content = draft.Content.Trim(),

            RequiresAcknowledgement =
                draft.RequiresAcknowledgement,

            VisibleToEmployees =
                draft.VisibleToEmployees,

            VisibleToHr =
                draft.VisibleToHr,

            VisibleToCeo =
                draft.VisibleToCeo,

            PublishedByUserId = actorUserId,

            PublishedAtUtc = now
        };

        _db.Entry(policy)
            .Property(x => x.RowVersion)
            .OriginalValue = rowVersion;

        _db.HrPolicyVersions.Add(publishedVersion);

        policy.CurrentPublishedVersion = nextVersion;

        policy.HasDraft = false;

        policy.DraftTitle = null;

        policy.DraftContent = null;

        policy.UpdatedByUserId = actorUserId;

        policy.UpdatedAtUtc = now;

        StagePolicyAudit(
            actorUserId,
            role!,
            "PolicyPublished",
            "HrPolicyVersion",
            publishedVersion.Id);

        try
        {
            // The new version, policy pointer and audit
            // event are committed atomically.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();

            return Fail<PolicyVersionResponse>(
                "Conflict",
                "Policy changed during publication. Reload and retry.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627)
        {
            _db.ChangeTracker.Clear();

            return Fail<PolicyVersionResponse>(
                "Conflict",
                "Policy version already exists. Reload and retry.");
        }

        return Ok(
            MapVersion(
                publishedVersion,
                new Dictionary<Guid, DateTime>()),
            "Policy version published.");
    }
}
