using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Policies;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Policies;

public sealed partial class HrPolicyService
{
    // API-050: Acknowledge one exact published version.
    public async Task<PolicyOperationResult<PolicyAcknowledgementResponse>>
        AcknowledgePolicyAsync(
            Guid actorUserId,
            Guid policyId,
            int version,
            CancellationToken cancellationToken = default)
    {
        var role = await GetActorRoleAsync(actorUserId);

        if (!CanAcknowledge(role))
        {
            return Fail<PolicyAcknowledgementResponse>(
                "Forbidden",
                "Policy acknowledgement is not permitted.");
        }

        if (version < 1)
        {
            return Fail<PolicyAcknowledgementResponse>(
                "ValidationError",
                "Invalid policy version.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var policyVisible = await VisiblePolicies(role)
                .AnyAsync(
                    x => x.Id == policyId,
                    cancellationToken);

            if (!policyVisible)
            {
                return Fail<PolicyAcknowledgementResponse>(
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
                return Fail<PolicyAcknowledgementResponse>(
                    "NotFound",
                    "Policy version was not found.");
            }

            if (!IsAcknowledgementAllowed(
                    publishedVersion,
                    role))
            {
                return Fail<PolicyAcknowledgementResponse>(
                    "Forbidden",
                    "Acknowledgement is not permitted for this version.");
            }

            var existing = await _db.PolicyAcknowledgements
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.PolicyVersionId == publishedVersion.Id &&
                         x.UserId == actorUserId,
                    cancellationToken);

            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);

                return Ok(
                    new PolicyAcknowledgementResponse(
                        policyId,
                        version,
                        DateTime.SpecifyKind(
                             existing.AcknowledgedAtUtc,
                             DateTimeKind.Utc)),
                    "Policy version already acknowledged.");
            }

            // New acknowledgements require prior
            // access by this user to this exact version.
            var hasOpenedVersion = await _db.AuditEvents
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

            if (!hasOpenedVersion)
            {
                return Fail<PolicyAcknowledgementResponse>(
                    "Conflict",
                    "Open the exact policy version before acknowledging.");
            }

            var acknowledgement = new PolicyAcknowledgement
            {
                PolicyVersionId = publishedVersion.Id,

                UserId = actorUserId,

                AcknowledgedAtUtc = DateTime.UtcNow
            };

            _db.PolicyAcknowledgements.Add(acknowledgement);

            StagePolicyAudit(
                actorUserId,
                role!,
                "PolicyAcknowledged",
                "PolicyAcknowledgement",
                acknowledgement.Id);

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Ok(
                new PolicyAcknowledgementResponse(
                    policyId,
                    version,
                    DateTime.SpecifyKind(
                         acknowledgement.AcknowledgedAtUtc,
                         DateTimeKind.Utc)),
                "Policy version acknowledged.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            // SQL Server automatically rolls back
            // transactions selected as deadlock victims.
            if (sql.Number != 1205)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }

            _db.ChangeTracker.Clear();

            return Fail<PolicyAcknowledgementResponse>(
                "Conflict",
                "Concurrent acknowledgement detected. Retry the request.");
        }
        catch (SqlException exception)
            when (exception.Number == 1205)
        {
            // SQL Server has rolled back the deadlock victim.
            _db.ChangeTracker.Clear();

            return Fail<PolicyAcknowledgementResponse>(
                "Conflict",
                "Concurrent policy operation detected. Retry the request.");
        }
    }
}
