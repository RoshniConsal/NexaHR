using System.Data;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Applications;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Domain.Rules.Applications;

namespace NexaHR.Infrastructure.Services.Applications;

public sealed partial class ApplicationService
{
    private static ApplicationWithdrawalOperationResponse
        WithdrawalFailure(string code, string message)
    {
        return new ApplicationWithdrawalOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    // API-077: Candidate-owned application withdrawal.
    public async Task<ApplicationWithdrawalOperationResponse>
        WithdrawAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationWithdrawalRequest request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithdrawCoreAsync(
                actorUserId,
                applicationId,
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _db.ChangeTracker.Clear();
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();

            return WithdrawalFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }
        catch (Exception ex) when (HasSqlConflict(ex))
        {
            _db.ChangeTracker.Clear();

            return WithdrawalFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return WithdrawalFailure(
                "application_database_error",
                "Unable to complete application withdrawal.");
        }
        catch (System.Data.Common.DbException)
        {
            _db.ChangeTracker.Clear();

            return WithdrawalFailure(
                "application_database_error",
                "Unable to confirm withdrawal. Refresh before retrying.");
        }
        catch (InvalidOperationException)
        {
            _db.ChangeTracker.Clear();

            return WithdrawalFailure(
                "application_source_invalid",
                "Application withdrawal could not be completed.");
        }
        catch (ArgumentException)
        {
            _db.ChangeTracker.Clear();

            return WithdrawalFailure(
                "application_source_invalid",
                "Application withdrawal could not be completed.");
        }
    }

    private async Task<ApplicationWithdrawalOperationResponse>
        WithdrawCoreAsync(
            Guid actorUserId,
            Guid applicationId,
            ApplicationWithdrawalRequest request,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Authorize before application lookup.
        var actor = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (actor is null ||
            !actor.IsActive ||
            !actor.EmailConfirmed ||
            await _users.IsLockedOutAsync(actor))
        {
            return WithdrawalFailure(
                "forbidden",
                "Access denied.");
        }

        var roles = await _users.GetRolesAsync(actor);

        if (roles.Count != 1 ||
            roles[0] != AppRoles.Candidate)
        {
            return WithdrawalFailure(
                "forbidden",
                "Candidate access is required.");
        }

        if (applicationId == Guid.Empty ||
            request is null ||
            !ApplicationWithdrawalRules.IsValidReason(
                request.Reason) ||
            !ApplicationWithdrawalRules.TryReadRowVersion(
                request.RowVersion,
                out var expectedVersion))
        {
            return WithdrawalFailure(
                "invalid_withdrawal_request",
                "A valid withdrawal reason and row version are required.");
        }

        // Ownership filter prevents application enumeration.
        var application = await _db.CandidateApplications
            .AsTracking()
            .SingleOrDefaultAsync(
                a => a.Id == applicationId &&
                     a.CandidateUserId == actorUserId,
                cancellationToken);

        if (application is null)
        {
            return WithdrawalFailure(
                "application_not_found",
                "Application not found.");
        }

        if (!application.RowVersion.SequenceEqual(
                expectedVersion))
        {
            return WithdrawalFailure(
                "application_conflict",
                "Application changed. Refresh and retry.");
        }

        var previousStage = application.Stage;

        if (!ApplicationWithdrawalRules.IsEligibleStage(
                previousStage))
        {
            return WithdrawalFailure(
                "application_withdrawal_not_allowed",
                "The application cannot be withdrawn in its current stage.");
        }

        // Temporary fail-closed integration guard.
        // Interview and Offer cancellation must be implemented
        // in the same transaction before these stages are enabled.
        if (ApplicationWithdrawalRules
                .RequiresCancellationIntegration(previousStage))
        {
            return WithdrawalFailure(
                "withdrawal_dependencies_unavailable",
                "Withdrawal is temporarily unavailable for this stage.");
        }

        // Also block earlier-stage applications with historical
        // interview or offer activity until integration is complete.
        var hasLaterStageHistory =
            await _db.ApplicationStageEvents
                .AsNoTracking()
                .AnyAsync(
                    e => e.ApplicationId == application.Id &&
                         (
                             e.ToStage == ApplicationStatuses.Interview ||
                             e.ToStage == ApplicationStatuses.OfferSent ||
                             e.ToStage == ApplicationStatuses.OfferAccepted ||
                             e.ToStage == ApplicationStatuses.Hired ||
                             e.ToStage == ApplicationStatuses.OfferDeclined
                         ),
                    cancellationToken);

        if (hasLaterStageHistory)
        {
            return WithdrawalFailure(
                "withdrawal_dependencies_unavailable",
                "Withdrawal requires recruitment cancellation integration.");
        }

        // Confirm the current stage matches the latest history.
        var latestEvent = await _db.ApplicationStageEvents
            .AsNoTracking()
            .Where(e => e.ApplicationId == application.Id)
            .OrderByDescending(e => e.AtUtc)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestEvent is null ||
            latestEvent.ToStage != previousStage)
        {
            return WithdrawalFailure(
                "application_source_invalid",
                "Application stage history could not be verified.");
        }

        var now = DateTime.UtcNow;

        if (now < latestEvent.AtUtc ||
            now < application.AppliedAtUtc)
        {
            return WithdrawalFailure(
                "application_source_invalid",
                "Application timestamps could not be verified.");
        }

        var reason = request.Reason.Trim();

        _db.Entry(application)
            .Property(a => a.RowVersion)
            .OriginalValue = expectedVersion;

        application.Stage = ApplicationStatuses.Withdrawn;
        application.UpdatedAtUtc = now;

        var stageEvent = new ApplicationStageEvent
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            ActorUserId = actorUserId,
            ActorRole = AppRoles.Candidate,
            FromStage = previousStage,
            ToStage = ApplicationStatuses.Withdrawn,
            Reason = reason,
            AtUtc = now
        };

        _db.ApplicationStageEvents.Add(stageEvent);

        // Do not copy the free-text reason into audit logs.
        _audit.StageEvent(new AuditWriteRequest
        {
            ActorUserId = actorUserId,
            ActorRole = AppRoles.Candidate,
            Module = "Recruitment",
            Action = "ApplicationWithdrawn",
            EntityType = "CandidateApplication",
            EntityId = application.Id,
            Outcome = "success"
        });

        _db.Notifications.Add(new Notification
        {
            RecipientUserId = actorUserId,
            Type = "ApplicationWithdrawn",
            Title = "Application withdrawn",
            Message = "Your job application has been withdrawn.",
            TargetType = null,
            TargetId = null,
            IsRead = false,
            CreatedAtUtc = now
        });

        // Preserve all existing application, CV, extraction,
        // review, matching, AI and consent history.

        await _db.SaveChangesAsync(cancellationToken);

        if (application.RowVersion.Length != 8 ||
            application.RowVersion.SequenceEqual(
                expectedVersion))
        {
            throw new InvalidOperationException(
                "Application concurrency token did not advance.");
        }

        var result = new ApplicationWithdrawalOperationResponse
        {
            Success = true,
            Code = "application_withdrawn",
            Message = "Application withdrawn successfully.",
            Data = new ApplicationWithdrawalResponse
            {
                ApplicationId = application.Id,
                EventId = stageEvent.Id,
                PreviousStage = previousStage,
                CurrentStage = application.Stage,
                AtUtc = now,
                RowVersion = Convert.ToBase64String(
                    application.RowVersion)
            }
        };

        await transaction.CommitAsync(
            CancellationToken.None);

        return result;
    }
}
