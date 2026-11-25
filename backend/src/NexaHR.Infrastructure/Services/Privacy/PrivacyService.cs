using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NexaHR.Application.DTOs.Privacy;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.Privacy;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Privacy;

public sealed partial class PrivacyService : IPrivacyService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;

    public PrivacyService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    private async Task<ApplicationUser?> GetActiveUserAsync(
        Guid userId)
    {
        if (userId == Guid.Empty)
            return null;

        var user = await _users.FindByIdAsync(
            userId.ToString());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
            return null;

        return user;
    }

    private async Task<bool> HasGovernanceAccessAsync(
        Guid actorId)
    {
        var user = await GetActiveUserAsync(actorId);

        if (user is null || !user.HasPrivacyGovernanceAccess)
            return false;

        var roles = await _users.GetRolesAsync(user);

        return roles.Count == 1 &&
            roles[0] == AppRoles.HRManager;
    }

    private static PrivacyOperationResponse<T> Success<T>(
        T data) where T : class
    {
        return new PrivacyOperationResponse<T>
        {
            Success = true,
            Code = "success",
            Message = "Privacy operation completed.",
            Data = data
        };
    }

    private static PrivacyOperationResponse<T> Failure<T>(
        string code,
        string message) where T : class
    {
        return new PrivacyOperationResponse<T>
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private async Task<ProcessingPolicy?> LatestPolicyAsync()
    {
        return await _db.ProcessingPolicies
            .AsNoTracking()
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync();
    }

    private static ProcessingPolicyResponse MapPolicy(
        ProcessingPolicy? policy,
        bool restricted)
    {
        if (policy is null)
        {
            return new ProcessingPolicyResponse
            {
                IsConfigured = false,
                IsRestricted = restricted,
                RealDataProcessingEnabled = false
            };
        }

        return new ProcessingPolicyResponse
        {
            IsConfigured = true,
            IsRestricted = restricted,

            // Recording a policy is not production approval.
            RealDataProcessingEnabled = false,

            PolicyId = restricted ? null : policy.Id,
            Version = policy.Version,

            CandidateRetentionDays = restricted
                ? null : policy.CandidateRetentionDays,

            EmploymentRetentionDays = restricted
                ? null : policy.EmploymentRetentionDays,

            AuditRetentionDays = restricted
                ? null : policy.AuditRetentionDays,

            CandidateRetentionAction = restricted
                ? null : policy.CandidateRetentionAction,

            EmploymentRetentionAction = restricted
                ? null : policy.EmploymentRetentionAction,

            AuditRetentionAction = restricted
                ? null : policy.AuditRetentionAction,

            ProcessingPurpose = restricted
                ? null : policy.ProcessingPurpose,

            DataCategories = restricted
                ? null : policy.DataCategories,

            ProviderName = restricted
                ? null : policy.ProviderName,

            ProviderRetention = restricted
                ? null : policy.ProviderRetention,

            ProviderTrainingAllowed = restricted
                ? null : policy.ProviderTrainingAllowed,

            ProviderAccessPolicy = restricted
                ? null : policy.ProviderAccessPolicy,

            ProcessingLocation = restricted
                ? null : policy.ProcessingLocation,

            LegalHoldPolicy = restricted
                ? null : policy.LegalHoldPolicy,

            ApprovalReference = restricted
                ? null : policy.ApprovalReference,

            ApprovedByUserId = restricted
                ? null : policy.ApprovedByUserId,

            ApprovedAtUtc = restricted
                ? null : policy.ApprovedAtUtc,

            RecordedByUserId = restricted
                ? null : policy.RecordedByUserId,

            CreatedAtUtc = restricted
                ? null : policy.CreatedAtUtc
        };
    }

    // API-154: Processing policy
    public async Task<
        PrivacyOperationResponse<ProcessingPolicyResponse>>
        GetProcessingPolicyAsync(Guid actorId)
    {
        var user = await GetActiveUserAsync(actorId);

        if (user is null)
        {
            return Failure<ProcessingPolicyResponse>(
                "forbidden",
                "Privacy access is not permitted.");
        }

        var roles = await _users.GetRolesAsync(user);

        if (roles.Count != 1 ||
            !AppRoles.All.Contains(roles[0]))
        {
            return Failure<ProcessingPolicyResponse>(
                "forbidden",
                "Privacy access is not permitted.");
        }

        var canGovern =
            roles[0] == AppRoles.HRManager &&
            user.HasPrivacyGovernanceAccess;

        var policy = await LatestPolicyAsync();

        return Success(
            MapPolicy(policy, !canGovern));
    }

    // API-155: Record a new policy version
    public async Task<
        PrivacyOperationResponse<ProcessingPolicyResponse>>
        UpdateProcessingPolicyAsync(
            Guid actorId,
            UpdateProcessingPolicyRequest request)
    {
        if (!await HasGovernanceAccessAsync(actorId))
        {
            return Failure<ProcessingPolicyResponse>(
                "forbidden",
                "Governance permission is required.");
        }

        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            true);

        if (!valid)
        {
            return Failure<ProcessingPolicyResponse>(
                "invalid_policy",
                "Complete all required policy fields.");
        }

        var requiredTexts = new[]
        {
            request.ProcessingPurpose,
            request.DataCategories,
            request.ProviderName,
            request.ProviderRetention,
            request.ProviderAccessPolicy,
            request.ProcessingLocation,
            request.LegalHoldPolicy,
            request.ApprovalReference
        };

        if (requiredTexts.Any(string.IsNullOrWhiteSpace))
        {
            return Failure<ProcessingPolicyResponse>(
                "invalid_policy",
                "Policy fields cannot be empty.");
        }

        // Do not allow a caller to impersonate another approver.
        if (request.ApprovedByUserId != actorId ||
            !request.ApprovedAtUtc.HasValue ||
            request.ApprovedAtUtc.Value.UtcDateTime >
                DateTime.UtcNow)
        {
            return Failure<ProcessingPolicyResponse>(
                "invalid_approval",
                "A valid approval attestation is required.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        var current = await LatestPolicyAsync();

        var currentVersion = current?.Version ?? 0;

        if (request.ExpectedVersion != currentVersion ||
            currentVersion == int.MaxValue)
        {
            return Failure<ProcessingPolicyResponse>(
                "version_conflict",
                "Reload the latest policy before updating.");
        }

        var now = DateTime.UtcNow;

        var policy = new ProcessingPolicy
        {
            Version = currentVersion + 1,

            CandidateRetentionDays =
                request.CandidateRetentionDays,

            EmploymentRetentionDays =
                request.EmploymentRetentionDays,

            AuditRetentionDays =
                request.AuditRetentionDays,

            CandidateRetentionAction =
                request.CandidateRetentionAction,

            EmploymentRetentionAction =
                request.EmploymentRetentionAction,

            AuditRetentionAction =
                request.AuditRetentionAction,

            ProcessingPurpose =
                request.ProcessingPurpose.Trim(),

            DataCategories =
                request.DataCategories.Trim(),

            ProviderName =
                request.ProviderName.Trim(),

            ProviderRetention =
                request.ProviderRetention.Trim(),

            ProviderTrainingAllowed =
                request.ProviderTrainingAllowed!.Value,

            ProviderAccessPolicy =
                request.ProviderAccessPolicy.Trim(),

            ProcessingLocation =
                request.ProcessingLocation.Trim(),

            LegalHoldPolicy =
                request.LegalHoldPolicy.Trim(),

            ApprovalReference =
                request.ApprovalReference.Trim(),

            ApprovedByUserId = actorId,

            ApprovedAtUtc =
                request.ApprovedAtUtc!.Value.UtcDateTime,

            RecordedByUserId = actorId,

            CreatedAtUtc = now
        };

        _db.ProcessingPolicies.Add(policy);

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Privacy",
            Action = "ProcessingPolicyRecorded",
            EntityType = "ProcessingPolicy",
            EntityId = policy.Id,
            Outcome = "success"
        });

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            return Failure<ProcessingPolicyResponse>(
                "version_conflict",
                "Reload the policy and retry.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Failure<ProcessingPolicyResponse>(
                "version_conflict",
                "Reload the policy and retry.");
        }

        return Success(MapPolicy(policy, false));
    }
}
