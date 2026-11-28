using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.Validation.CvReviews;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Services.Applications;

namespace NexaHR.Infrastructure.Services.CvReviews;

public sealed partial class CvReviewService
{
    private static CvReviewConfirmationOperationResponse
        ConfirmationFailure(
            string code,
            string message,
            IReadOnlyList<CvFieldValidationError>? errors = null)
    {
        return new CvReviewConfirmationOperationResponse
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors ?? []
        };
    }

    private static CvReviewConfirmationOperationResponse
        ConfirmationConflict()
    {
        return ConfirmationFailure(
            "cv_review_conflict",
            "CV review data has changed. Refresh before retrying.");
    }

    // API-081: Confirm the exact saved draft.
    // Never replace client-reviewed values during confirmation.
    public async Task<CvReviewConfirmationOperationResponse>
        ConfirmReviewAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewConfirmationRequest request,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await ConfirmReviewCoreAsync(
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
            return ConfirmationConflict();
        }
        catch (Exception exception)
            when (IsDraftSqlConflict(exception))
        {
            _db.ChangeTracker.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            return ConfirmationConflict();
        }
        catch (JsonException)
        {
            _db.ChangeTracker.Clear();

            return ConfirmationFailure(
                "cv_review_json_invalid",
                "Stored CV review data is invalid.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return ConfirmationFailure(
                "cv_review_database_error",
                "Unable to confirm the operation result. Refresh before retrying.");
        }
        catch (DbException)
        {
            _db.ChangeTracker.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            return ConfirmationFailure(
                "cv_review_database_error",
                "Unable to confirm the operation result. Refresh before retrying.");
        }
        catch (InvalidOperationException)
        {
            _db.ChangeTracker.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            return ConfirmationFailure(
                "cv_review_source_invalid",
                "CV review source data is invalid or unavailable.");
        }
    }

    private async Task<CvReviewConfirmationOperationResponse>
        ConfirmReviewCoreAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewConfirmationRequest request,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Authorization precedes application existence checks.
        if (!await IsAuthorizedHrAsync(
                actorUserId, cancellationToken))
        {
            return ConfirmationFailure(
                "forbidden",
                "You are not authorized to confirm CV reviews.");
        }

        if (applicationId == Guid.Empty ||
            request is null ||
            request.DocumentId == Guid.Empty ||
            request.DocumentVersion < 1 ||
            request.VacancyRevisionId == Guid.Empty ||
            request.ExpectedReviewVersionId == Guid.Empty ||
            request.ExpectedExtractionRunId == Guid.Empty)
        {
            return ConfirmationFailure(
                "invalid_cv_review_request",
                "Valid application and review references are required.");
        }

        if (!TryReadDraftRowVersion(
                request.ApplicationRowVersion,
                out var expectedApplicationVersion) ||
            !TryReadDraftRowVersion(
                request.ExpectedReviewRowVersion,
                out var expectedReviewVersion))
        {
            return ConfirmationFailure(
                "invalid_cv_review_request",
                "Valid application and review concurrency tokens are required.");
        }

        byte[] expectedExtractionVersion = [];

        if (request.ExpectedExtractionRunId.HasValue)
        {
            if (!TryReadDraftRowVersion(
                    request.ExpectedExtractionRowVersion,
                    out expectedExtractionVersion))
            {
                return ConfirmationFailure(
                    "invalid_cv_review_request",
                    "A valid extraction concurrency token is required.");
            }
        }
        else if (request.ExpectedExtractionRowVersion is not null)
        {
            return ConfirmationFailure(
                "invalid_cv_review_request",
                "Extraction concurrency references are inconsistent.");
        }

        var workspace = await LoadWorkspaceAsync(
            applicationId,
            cancellationToken);

        if (workspace is null)
        {
            return ConfirmationFailure(
                "application_not_found",
                "Application was not found.");
        }

        var application = workspace.Application;
        var document = workspace.Document;
        var previous = workspace.LatestReview;
        var extraction = workspace.LatestExtraction;

        if (!IsDraftStageAllowed(application.Stage))
        {
            return ConfirmationFailure(
                "cv_review_stage_conflict",
                "The application stage does not permit CV confirmation.");
        }

        ValidateWorkspaceSources(workspace);

        if (application.RowVersion.Length != 8)
        {
            return ConfirmationFailure(
                "cv_review_source_invalid",
                "Application concurrency data is invalid.");
        }

        if (!application.RowVersion.SequenceEqual(
                expectedApplicationVersion))
        {
            return ConfirmationConflict();
        }

        if (previous is null)
        {
            return ConfirmationFailure(
                "cv_review_draft_required",
                "Save a CV review draft before confirmation.");
        }

        if (previous.Id != request.ExpectedReviewVersionId ||
            !previous.RowVersion.SequenceEqual(
                expectedReviewVersion) ||
            extraction?.Id != request.ExpectedExtractionRunId ||
            (extraction is not null &&
             !extraction.RowVersion.SequenceEqual(
                 expectedExtractionVersion)))
        {
            return ConfirmationConflict();
        }

        if (previous.Status != CvReviewStatuses.Draft)
        {
            return ConfirmationFailure(
                "cv_review_already_confirmed",
                "The latest review is already confirmed.");
        }

        if (previous.Version == int.MaxValue)
        {
            return ConfirmationFailure(
                "cv_review_source_invalid",
                "Review version data is invalid.");
        }

        if (document.Id != request.DocumentId ||
            document.Version != request.DocumentVersion ||
            workspace.ReviewRevision.Id !=
                request.VacancyRevisionId ||
            previous.VacancyRevisionId !=
                request.VacancyRevisionId)
        {
            return ConfirmationConflict();
        }

        // Confirm only a draft based on the current extraction.
        // A new extraction requires another draft review.
        if (previous.ExtractionRunId != extraction?.Id)
        {
            return ConfirmationFailure(
                "cv_review_conflict",
                "The extraction source has changed. Save a new draft.");
        }

        if (!HasExactApproval(
                workspace.ApplicationRevision,
                workspace.RevisionDecisions) ||
            !HasExactApproval(
                workspace.ReviewRevision,
                workspace.RevisionDecisions))
        {
            return ConfirmationFailure(
                "cv_review_revision_not_approved",
                "The required vacancy revision is not approved.");
        }

        if (document.ScanStatus != CvScanStatuses.Clean ||
            !document.ScannedAtUtc.HasValue ||
            document.Sha256Hash.Length != 64 ||
            !document.Sha256Hash.All(Uri.IsHexDigit))
        {
            return ConfirmationFailure(
                "cv_review_document_not_available",
                "The submitted CV is not available for review.");
        }

        if (extraction is not null)
        {
            if (extraction.Status !=
                    CvExtractionStatuses.ReadyForReview &&
                extraction.Status !=
                    CvExtractionStatuses.Failed)
            {
                return ConfirmationFailure(
                    "cv_review_extraction_pending",
                    "Wait for the current extraction to finish.");
            }

            // Reuse strict extraction JSON and evidence mapping.
            _ = MapExtraction(extraction);

            if (extraction.CompletedAtUtc is null ||
                previous.CreatedAtUtc <
                    extraction.CompletedAtUtc.Value)
            {
                return ConfirmationFailure(
                    "cv_review_source_invalid",
                    "The saved review extraction history is invalid.");
            }
        }

        // Validate the actual stored draft, not client values.
        _ = MapReview(previous);

        var values = CvReviewJsonReader.ReadValues(
            previous.ValuesJson);

        var resolution =
            CvReviewJsonReader.ReadDuplicateResolution(
                previous.DuplicateResolutionJson);

        var errors = CvStructuredValuesValidator
            .ValidateConfirmation(values);

        if (errors.Count != 0)
        {
            return ConfirmationFailure(
                "invalid_cv_review_values",
                "Complete the required CV review fields.",
                errors);
        }

        // Recheck contact duplicates inside the same transaction.
        var duplicates =
            await CandidateContactDuplicateChecker.CheckAsync(
                _db,
                _users,
                application.CandidateUserId,
                values.Email,
                values.PhoneNumber,
                cancellationToken);

        var duplicateErrors =
            CvReviewMetadataValidator
                .ValidateDuplicateConfirmation(
                    resolution,
                    duplicates.EmailMatch,
                    duplicates.PhoneMatch);

        if (duplicateErrors.Count != 0)
        {
            return ConfirmationFailure(
                "invalid_cv_review_duplicate_resolution",
                "Resolve the current duplicate contact flags before confirmation.",
                duplicateErrors);
        }

        // Validate direct history linkage before appending.
        if (previous.PreviousReviewVersionId.HasValue)
        {
            var parent = await _db.CvReviewVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id ==
                        previous.PreviousReviewVersionId.Value,
                    cancellationToken);

            if (parent is null ||
                parent.ApplicationId != application.Id ||
                parent.DocumentId != document.Id ||
                parent.Version != previous.Version - 1 ||
                parent.CreatedAtUtc > previous.CreatedAtUtc)
            {
                return ConfirmationFailure(
                    "cv_review_source_invalid",
                    "CV review history is invalid.");
            }
        }

        // Verify actual private CV bytes before any persistence.
        if (!await VerifyReviewCvIntegrityAsync(
                document,
                cancellationToken))
        {
            return ConfirmationFailure(
                "cv_review_document_not_available",
                "The submitted CV is unavailable or failed integrity verification.");
        }

        var now = DateTime.UtcNow;

        if (previous.CreatedAtUtc > now)
        {
            return ConfirmationFailure(
                "cv_review_source_invalid",
                "CV review chronology is invalid.");
        }

        var confirmed = new CvReviewVersion
        {
            ApplicationId = application.Id,
            DocumentId = document.Id,
            VacancyRevisionId = previous.VacancyRevisionId,
            ExtractionRunId = previous.ExtractionRunId,
            ReviewerUserId = actorUserId,
            PreviousReviewVersionId = previous.Id,
            Version = previous.Version + 1,
            Status = CvReviewStatuses.Confirmed,

            // Preserve the exact saved draft content.
            ValuesJson = previous.ValuesJson,
            DuplicateResolutionJson =
                previous.DuplicateResolutionJson,

            // Confirmation itself changes no reviewed field.
            ChangedFieldsJson = "[]",

            CreatedAtUtc = now,
            ConfirmedAtUtc = now
        };

        _db.CandidateApplications.Attach(application);

        var entry = _db.Entry(application);

        entry.Property(a => a.RowVersion).OriginalValue =
            expectedApplicationVersion;

        application.UpdatedAtUtc = now;

        entry.Property(a => a.UpdatedAtUtc)
            .IsModified = true;

        // Do not change the application stage.
        // Do not modify the previous draft.
        _db.CvReviewVersions.Add(confirmed);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "CvReviewConfirmed",
                EntityType = "CvReviewVersion",
                EntityId = confirmed.Id,
                Outcome = "success"
            });
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                "CV confirmation audit event could not be staged.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (application.RowVersion.Length != 8 ||
            confirmed.RowVersion.Length != 8 ||
            application.RowVersion.SequenceEqual(
                expectedApplicationVersion))
        {
            throw new InvalidOperationException(
                "Confirmation concurrency token generation failed.");
        }

        var mappedReview = MapReview(confirmed)
            ?? throw new InvalidOperationException(
                "Confirmation response mapping failed.");

        var response = new CvReviewConfirmationResponse
        {
            ApplicationId = application.Id,
            DocumentId = document.Id,
            VacancyRevisionId = confirmed.VacancyRevisionId,
            ApplicationRowVersion =
                Convert.ToBase64String(
                    application.RowVersion),

            Stage = application.Stage,

            Review = mappedReview,

            DuplicateFlags =
                new CvReviewDuplicateFlagsResponse
                {
                    DuplicateEmailFlag =
                        duplicates.EmailMatch,

                    DuplicatePhoneFlag =
                        duplicates.PhoneMatch,

                    Source = "CurrentReview"
                }
        };

        cancellationToken.ThrowIfCancellationRequested();

        await transaction.CommitAsync(cancellationToken);

        return new CvReviewConfirmationOperationResponse
        {
            Success = true,
            Code = "cv_review_confirmed",
            Message = "CV review confirmed successfully.",
            Data = response
        };
    }
}
