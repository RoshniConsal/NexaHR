using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NexaHR.Application.DTOs.Privacy;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.Privacy;

public sealed partial class PrivacyService
{
    private static bool ValidRequest(object request)
    {
        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            true);
    }

    private static byte[]? ParseHoldVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(value);

            return bytes.Length == 8
                ? bytes
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static LegalHoldResponse MapHold(
        LegalHold hold)
    {
        return new LegalHoldResponse
        {
            LegalHoldId = hold.Id,
            SubjectId = hold.SubjectId,
            Scope = hold.Scope,
            IsActive = hold.IsActive,
            CreatedAtUtc = DateTime.SpecifyKind(hold.CreatedAtUtc, DateTimeKind.Utc),
            UpdatedAtUtc = hold.UpdatedAtUtc.HasValue
                ? DateTime.SpecifyKind(hold.UpdatedAtUtc.Value, DateTimeKind.Utc)
                : null,
            ReleasedAtUtc = hold.ReleasedAtUtc.HasValue
                ? DateTime.SpecifyKind(hold.ReleasedAtUtc.Value, DateTimeKind.Utc)
                : null,
            RowVersion = Convert.ToBase64String(
                hold.RowVersion)
        };
    }

    private void StageHoldEvent(
        LegalHold hold,
        Guid actorId,
        string action,
        string reason,
        DateTime now)
    {
        _db.LegalHoldEvents.Add(new LegalHoldEvent
        {
            LegalHoldId = hold.Id,
            SubjectId = hold.SubjectId,
            ActorUserId = actorId,
            Action = action,
            Scope = hold.Scope,
            Reason = reason,
            CreatedAtUtc = now
        });

        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorId,
            ActorRole = AppRoles.HRManager,
            Module = "Privacy",
            Action = action,
            EntityType = "LegalHold",
            EntityId = hold.Id,
            Outcome = "success"
        });
    }

    // API-159: Place or update legal hold
    public async Task<
        PrivacyOperationResponse<LegalHoldResponse>>
        UpsertLegalHoldAsync(
            Guid actorId,
            Guid subjectId,
            UpsertLegalHoldRequest request)
    {
        if (!await HasGovernanceAccessAsync(actorId))
        {
            return Failure<LegalHoldResponse>(
                "forbidden",
                "Governance permission is required.");
        }

        if (!ValidRequest(request) ||
            subjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length < 3)
        {
            return Failure<LegalHoldResponse>(
                "invalid_request",
                "A valid scope and reason are required.");
        }

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var subject = await _users.FindByIdAsync(
                subjectId.ToString());

            if (subject is null)
            {
                return Failure<LegalHoldResponse>(
                    "not_found",
                    "Subject was not found.");
            }

            var hold = await _db.LegalHolds
                .SingleOrDefaultAsync(h =>
                    h.SubjectId == subjectId);

            var now = DateTime.UtcNow;
            var reason = request.Reason.Trim();
            string action;

            if (hold is null)
            {
                if (!string.IsNullOrEmpty(
                    request.RowVersion))
                {
                    return Failure<LegalHoldResponse>(
                        "version_conflict",
                        "Reload legal hold information.");
                }

                hold = new LegalHold
                {
                    SubjectId = subjectId,
                    Scope = request.Scope,
                    Reason = reason,
                    IsActive = true,
                    CreatedByUserId = actorId,
                    CreatedAtUtc = now
                };

                _db.LegalHolds.Add(hold);

                action = "LegalHoldPlaced";
            }
            else
            {
                var version = ParseHoldVersion(
                    request.RowVersion);

                if (version is null ||
                    !hold.RowVersion.SequenceEqual(version))
                {
                    return Failure<LegalHoldResponse>(
                        "version_conflict",
                        "Reload the legal hold before updating.");
                }

                // Do not silently narrow an active hold.
                if (hold.IsActive &&
                    hold.Scope != request.Scope &&
                    request.Scope != "All")
                {
                    return Failure<LegalHoldResponse>(
                        "scope_conflict",
                        "An active hold cannot be narrowed directly.");
                }

                _db.Entry(hold)
                    .Property(h => h.RowVersion)
                    .OriginalValue = version;

                action = hold.IsActive
                    ? "LegalHoldUpdated"
                    : "LegalHoldPlaced";

                hold.Scope = request.Scope;
                hold.Reason = reason;
                hold.IsActive = true;
                hold.UpdatedByUserId = actorId;
                hold.UpdatedAtUtc = now;

                hold.ReleasedByUserId = null;
                hold.ReleasedAtUtc = null;
            }

            StageHoldEvent(
                hold,
                actorId,
                action,
                reason,
                now);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Success(MapHold(hold));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<LegalHoldResponse>(
                "version_conflict",
                "Reload the legal hold and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627 or 1205)
        {
            return Failure<LegalHoldResponse>(
                "version_conflict",
                "Reload the legal hold and retry.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Failure<LegalHoldResponse>(
                "version_conflict",
                "Reload the legal hold and retry.");
        }
    }

    // API-160: Release hold; preserve all history
    public async Task<
        PrivacyOperationResponse<LegalHoldResponse>>
        ReleaseLegalHoldAsync(
            Guid actorId,
            Guid subjectId,
            ReleaseLegalHoldRequest request)
    {
        if (!await HasGovernanceAccessAsync(actorId))
        {
            return Failure<LegalHoldResponse>(
                "forbidden",
                "Governance permission is required.");
        }

        if (!ValidRequest(request) ||
            subjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length < 3)
        {
            return Failure<LegalHoldResponse>(
                "invalid_request",
                "A valid release reason is required.");
        }

        var version = ParseHoldVersion(
            request.RowVersion);

        if (version is null)
        {
            return Failure<LegalHoldResponse>(
                "invalid_version",
                "A valid row version is required.");
        }

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var hold = await _db.LegalHolds
                .SingleOrDefaultAsync(h =>
                    h.SubjectId == subjectId);

            if (hold is null)
            {
                return Failure<LegalHoldResponse>(
                    "not_found",
                    "Legal hold was not found.");
            }

            if (!hold.RowVersion.SequenceEqual(version))
            {
                return Failure<LegalHoldResponse>(
                    "version_conflict",
                    "Reload the legal hold before releasing.");
            }

            if (!hold.IsActive)
            {
                return Success(MapHold(hold));
            }

            _db.Entry(hold)
                .Property(h => h.RowVersion)
                .OriginalValue = version;

            var now = DateTime.UtcNow;

            hold.IsActive = false;
            hold.ReleasedByUserId = actorId;
            hold.ReleasedAtUtc = now;
            hold.UpdatedByUserId = actorId;
            hold.UpdatedAtUtc = now;

            StageHoldEvent(
                hold,
                actorId,
                "LegalHoldReleased",
                request.Reason.Trim(),
                now);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Success(MapHold(hold));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<LegalHoldResponse>(
                "version_conflict",
                "Reload the legal hold and retry.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number == 1205)
        {
            return Failure<LegalHoldResponse>(
                "version_conflict",
                "Reload the legal hold and retry.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Failure<LegalHoldResponse>(
                "version_conflict",
                "Reload the legal hold and retry.");
        }
    }
}
