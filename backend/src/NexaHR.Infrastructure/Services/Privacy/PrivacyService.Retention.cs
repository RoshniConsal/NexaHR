using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Privacy;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Privacy;

public sealed partial class PrivacyService
{
    private static (int Days, string Action)?
        GetRetentionSchedule(
            ProcessingPolicy policy,
            string category)
    {
        return category switch
        {
            "Candidate" => (
                policy.CandidateRetentionDays,
                policy.CandidateRetentionAction),

            "Employment" => (
                policy.EmploymentRetentionDays,
                policy.EmploymentRetentionAction),

            "Audit" => (
                policy.AuditRetentionDays,
                policy.AuditRetentionAction),

            _ => null
        };
    }

    private static bool IsValidSchedule(
        int days,
        string action)
    {
        return days >= 1 &&
            days <= 36500 &&
            (action == "Delete" ||
             action == "Anonymise");
    }

    // API-156: Read-only retention eligibility preview
    public async Task<
        PrivacyOperationResponse<RetentionPreviewResponse>>
        PreviewRetentionAsync(
            Guid actorId,
            RetentionPreviewRequest request)
    {
        if (!await HasGovernanceAccessAsync(actorId))
        {
            return Failure<RetentionPreviewResponse>(
                "forbidden",
                "Governance permission is required.");
        }

        if (!ValidRequest(request))
        {
            return Failure<RetentionPreviewResponse>(
                "invalid_request",
                "Invalid retention preview request.");
        }

        var policy = await LatestPolicyAsync();

        if (policy is null)
        {
            return Failure<RetentionPreviewResponse>(
                "policy_not_configured",
                "Record a processing policy first.");
        }

        if (policy.Version != request.PolicyVersion)
        {
            return Failure<RetentionPreviewResponse>(
                "version_conflict",
                "Reload the latest processing policy.");
        }

        var schedule = GetRetentionSchedule(
            policy,
            request.DataCategory);

        if (schedule is null ||
            !IsValidSchedule(
                schedule.Value.Days,
                schedule.Value.Action))
        {
            return Failure<RetentionPreviewResponse>(
                "invalid_schedule",
                "Retention schedule is invalid.");
        }

        var cutoff = DateTime.UtcNow.AddDays(
            -schedule.Value.Days);

        // Do not invent eligibility counts or assume
        // subject relationships are already integrated.
        return Success(new RetentionPreviewResponse
        {
            PolicyVersion = policy.Version,
            DataCategory = request.DataCategory,
            Action = schedule.Value.Action,
            CutoffUtc = cutoff,
            MaxRecords = request.MaxRecords,

            EligibleCount = null,
            HeldCount = null,

            CanExecute = false,
            Status = "Blocked",
            BlockReason = "retention_integration_pending"
        });
    }

    // API-157: Explicit retention run authorization
    public async Task<
        PrivacyOperationResponse<RetentionRunResponse>>
        CreateRetentionRunAsync(
            Guid actorId,
            CreateRetentionRunRequest request)
    {
        if (!await HasGovernanceAccessAsync(actorId))
        {
            return Failure<RetentionRunResponse>(
                "forbidden",
                "Governance permission is required.");
        }

        if (!ValidRequest(request) ||
            string.IsNullOrWhiteSpace(
                request.AuthorizationReason) ||
            request.AuthorizationReason.Trim().Length < 3)
        {
            return Failure<RetentionRunResponse>(
                "invalid_request",
                "A valid authorization reason is required.");
        }

        if (request.ConfirmAuthorization != true)
        {
            return Failure<RetentionRunResponse>(
                "authorization_required",
                "Explicit retention authorization is required.");
        }

        var policy = await LatestPolicyAsync();

        if (policy is null)
        {
            return Failure<RetentionRunResponse>(
                "policy_not_configured",
                "Record a processing policy first.");
        }

        if (policy.Version != request.PolicyVersion)
        {
            return Failure<RetentionRunResponse>(
                "version_conflict",
                "Reload the latest processing policy.");
        }

        var schedule = GetRetentionSchedule(
            policy,
            request.DataCategory);

        if (schedule is null ||
            !IsValidSchedule(
                schedule.Value.Days,
                schedule.Value.Action))
        {
            return Failure<RetentionRunResponse>(
                "invalid_schedule",
                "Retention schedule is invalid.");
        }

        // Fail closed until:
        // - company approval evidence is verified
        // - eligibility queries are integrated
        // - legal holds are rechecked transactionally
        // - bounded execution worker is tested
        //
        // No executable run is queued at this stage.
        return Failure<RetentionRunResponse>(
            "retention_integration_pending",
            "Retention execution is not yet enabled.");
    }

    // API-158: Authorized retention run evidence
    public async Task<
        PrivacyOperationResponse<RetentionRunResponse>>
        GetRetentionRunAsync(
            Guid actorId,
            Guid runId)
    {
        if (!await HasGovernanceAccessAsync(actorId))
        {
            return Failure<RetentionRunResponse>(
                "forbidden",
                "Governance permission is required.");
        }

        if (runId == Guid.Empty)
        {
            return Failure<RetentionRunResponse>(
                "invalid_request",
                "A valid run identifier is required.");
        }

        var result = await (
            from run in _db.RetentionRuns.AsNoTracking()

            join policy in
                _db.ProcessingPolicies.AsNoTracking()
                on run.ProcessingPolicyId equals policy.Id

            where run.Id == runId

            select new RetentionRunResponse
            {
                RunId = run.Id,
                PolicyVersion = policy.Version,
                DataCategory = run.DataCategory,
                Action = run.Action,
                Status = run.Status,
                MaxRecords = run.MaxRecords,

                EligibleCount =
                    run.Status == "Pending" ||
                    run.Status == "Blocked"
                    ? (int?)null
                    : run.EligibleCount,

                ProcessedCount = run.ProcessedCount,

                HeldCount =
                    run.Status == "Pending" ||
                    run.Status == "Blocked"
                    ? (int?)null
                    : run.HeldCount,

                FailedCount = run.FailedCount,
                CutoffUtc = run.CutoffUtc,
                CreatedAtUtc = run.CreatedAtUtc,
                StartedAtUtc = run.StartedAtUtc,
                CompletedAtUtc = run.CompletedAtUtc,

                ResultCode = null
            }
        ).SingleOrDefaultAsync();

        if (result is null)
        {
            return Failure<RetentionRunResponse>(
                "not_found",
                "Retention run was not found.");
        }

        return Success(result);
    }
}
