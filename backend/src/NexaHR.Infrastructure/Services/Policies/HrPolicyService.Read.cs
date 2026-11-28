using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Policies;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Policies;

public sealed partial class HrPolicyService
{
    private IQueryable<HrPolicyVersion> VisibleVersions(
        string? role)
    {
        var query = _db.HrPolicyVersions.AsNoTracking();

        return role switch
        {
            AppRoles.HRManager => query,

            AppRoles.Employee => query.Where(
                x => x.VisibleToEmployees),

            AppRoles.CEO => query.Where(
                x => x.VisibleToCeo),

            _ => query.Where(x => false)
        };
    }

    private IQueryable<HrPolicy> VisiblePolicies(
        string? role)
    {
        var query = _db.HrPolicies.AsNoTracking();

        if (CanManage(role))
            return query;

        var versions = VisibleVersions(role);

        return query.Where(policy =>
            versions.Any(version =>
                version.PolicyId == policy.Id &&
                version.Version ==
                    policy.CurrentPublishedVersion));
    }

    private async Task<Dictionary<Guid, DateTime>>
        GetOwnAcknowledgementsAsync(
            Guid actorUserId,
            IReadOnlyCollection<Guid> versionIds,
            CancellationToken cancellationToken)
    {
        if (versionIds.Count == 0)
            return new Dictionary<Guid, DateTime>();

        return await _db.PolicyAcknowledgements
            .AsNoTracking()
            .Where(x =>
                x.UserId == actorUserId &&
                versionIds.Contains(x.PolicyVersionId))
            .ToDictionaryAsync(
                x => x.PolicyVersionId,
                x => x.AcknowledgedAtUtc,
                cancellationToken);
    }

    private static PolicyVersionResponse MapVersion(
        HrPolicyVersion version,
        IReadOnlyDictionary<Guid, DateTime> acknowledgements)
    {
        var acknowledged = acknowledgements.TryGetValue(
            version.Id,
            out var acknowledgedAt);

        return new PolicyVersionResponse(
            version.PolicyId,
            version.Version,
            version.Title,
            version.Content,
            version.RequiresAcknowledgement,
            DateTime.SpecifyKind(
                    version.PublishedAtUtc,
                    DateTimeKind.Utc),
            acknowledged,
            acknowledged
                    ? DateTime.SpecifyKind(
                        acknowledgedAt,
                        DateTimeKind.Utc)
                    : null);
    }

    // API-044: List visible policies.
    public async Task<PolicyOperationResult<PolicyListResponse>>
        GetPoliciesAsync(
            Guid actorUserId,
            PolicyListQuery query,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanRead(role))
        {
            return Fail<PolicyListResponse>(
                "Forbidden",
                "Policy access is not permitted.");
        }

        if (!ValidPage(query.Page, query.PageSize))
        {
            return Fail<PolicyListResponse>(
                "ValidationError",
                "Invalid pagination.");
        }

        var search = query.Search?.Trim();

        if (search is { Length: > 200 })
        {
            return Fail<PolicyListResponse>(
                "ValidationError",
                "Search must not exceed 200 characters.");
        }

        var isHr = CanManage(role);

        var policies = VisiblePolicies(role);

        var versions = VisibleVersions(role);

        var currentVersions =
            from policy in policies
            from version in versions
                .Where(v =>
                    v.PolicyId == policy.Id &&
                    v.Version == policy.CurrentPublishedVersion)
                .DefaultIfEmpty()
            select new
            {
                Policy = policy,
                Version = version,
                Title = version != null
                    ? version.Title
                    : isHr
                        ? policy.DraftTitle ?? string.Empty
                        : string.Empty
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            currentVersions = currentVersions.Where(
                x => x.Title.Contains(search));
        }

        var totalCount = await currentVersions
            .CountAsync(cancellationToken);

        var rows = await currentVersions
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Policy.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var versionIds = rows
            .Where(x => x.Version != null)
            .Select(x => x.Version!.Id)
            .ToArray();

        var acknowledgements =
            await GetOwnAcknowledgementsAsync(
                actorUserId,
                versionIds,
                cancellationToken);

        var items = rows.Select(x =>
            new PolicyListItem(
                x.Policy.Id,
                x.Title,
                x.Policy.CurrentPublishedVersion,
                x.Version != null
                    ? x.Version.RequiresAcknowledgement
                    : isHr &&
                        x.Policy.DraftRequiresAcknowledgement,
                x.Version != null &&
                    acknowledgements.ContainsKey(x.Version.Id),
                x.Version is null
                    ? null
                    : DateTime.SpecifyKind(
                        x.Version.PublishedAtUtc,
                        DateTimeKind.Utc),
                isHr ? x.Policy.HasDraft : null))
            .ToList();

        return Ok(new PolicyListResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount));
    }

    // API-046: Policy details and visible version history.
    public async Task<PolicyOperationResult<PolicyDetailsResponse>>
        GetPolicyAsync(
            Guid actorUserId,
            Guid policyId,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanRead(role))
        {
            return Fail<PolicyDetailsResponse>(
                "Forbidden",
                "Policy access is not permitted.");
        }

        var policy = await VisiblePolicies(role)
            .SingleOrDefaultAsync(
                x => x.Id == policyId,
                cancellationToken);

        if (policy is null)
        {
            return Fail<PolicyDetailsResponse>(
                "NotFound",
                "Policy was not found.");
        }

        var versions = await VisibleVersions(role)
            .Where(x => x.PolicyId == policyId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        var versionIds = versions
            .Select(x => x.Id)
            .ToArray();

        var acknowledgements =
            await GetOwnAcknowledgementsAsync(
                actorUserId,
                versionIds,
                cancellationToken);

        var current = versions.SingleOrDefault(
            x => x.Version == policy.CurrentPublishedVersion);

        if (!CanManage(role) && current is null)
        {
            return Fail<PolicyDetailsResponse>(
                "NotFound",
                "Policy was not found.");
        }

        var history = versions.Select(version =>
        {
            var acknowledged =
                acknowledgements.TryGetValue(
                    version.Id,
                    out var acknowledgedAt);

            return new PolicyVersionSummary(
                version.Version,
                version.Title,
                version.RequiresAcknowledgement,
                DateTime.SpecifyKind(
                    version.PublishedAtUtc,
                    DateTimeKind.Utc),
                acknowledged,
                acknowledged
                    ? DateTime.SpecifyKind(
                        acknowledgedAt,
                        DateTimeKind.Utc)
                    : null);
        }).ToList();

        PolicyDraftResponse? draft = null;

        if (CanManage(role) && policy.HasDraft)
        {
            draft = new PolicyDraftResponse(
                policy.DraftTitle ?? string.Empty,
                policy.DraftContent ?? string.Empty,
                policy.DraftRequiresAcknowledgement,
                policy.DraftVisibleToEmployees,
                policy.DraftVisibleToHr,
                policy.DraftVisibleToCeo);
        }

        return Ok(new PolicyDetailsResponse(
            policy.Id,
            policy.CurrentPublishedVersion,
            current is null
                ? null
                : MapVersion(current, acknowledgements),
            history,
            draft,
            CanManage(role)
                ? Convert.ToBase64String(policy.RowVersion)
                : null));
    }

    // API-049: Retrieve one exact published policy version.
    public async Task<PolicyOperationResult<PolicyVersionResponse>>
        GetPolicyVersionAsync(
            Guid actorUserId,
            Guid policyId,
            int version,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanRead(role))
        {
            return Fail<PolicyVersionResponse>(
                "Forbidden",
                "Policy access is not permitted.");
        }

        if (version < 1)
        {
            return Fail<PolicyVersionResponse>(
                "ValidationError",
                "Invalid policy version.");
        }

        var policyVisible = await VisiblePolicies(role)
            .AnyAsync(
                x => x.Id == policyId,
                cancellationToken);

        if (!policyVisible)
        {
            return Fail<PolicyVersionResponse>(
                "NotFound",
                "Policy version was not found.");
        }

        var publishedVersion = await VisibleVersions(role)
            .SingleOrDefaultAsync(
                x => x.PolicyId == policyId &&
                     x.Version == version,
                cancellationToken);

        if (publishedVersion is null)
        {
            return Fail<PolicyVersionResponse>(
                "NotFound",
                "Policy version was not found.");
        }

        var acknowledgements =
            await GetOwnAcknowledgementsAsync(
                actorUserId,
                new[] { publishedVersion.Id },
                cancellationToken);

        // Record server-side access evidence for
        // this authenticated user and exact version.
        if (IsAcknowledgementAllowed(publishedVersion, role))
        {
            var alreadyOpened = await _db.AuditEvents
                .AsNoTracking()
                .AnyAsync(a =>
                    a.ActorUserId == actorUserId &&
                    a.ActorRole == role &&
                    a.Module == "Policies" &&
                    a.Action == "PolicyVersionOpened" &&
                    a.EntityType == "HrPolicyVersion" &&
                    a.EntityId == publishedVersion.Id &&
                    a.Outcome == "success",
                    cancellationToken);

            if (!alreadyOpened)
            {
                StagePolicyAudit(
                    actorUserId,
                    role!,
                    "PolicyVersionOpened",
                    "HrPolicyVersion",
                    publishedVersion.Id);

                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(MapVersion(
            publishedVersion,
            acknowledgements));
    }
}
