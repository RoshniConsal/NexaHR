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
    private static CvReviewRevalidationOperationResponse
        RevalidationFailure(
            string code,
            string message,
            IReadOnlyList<CvFieldValidationError>? errors = null)
    {
        return new CvReviewRevalidationOperationResponse
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors ?? []
        };
    }

    private static CvReviewRevalidationOperationResponse
        RevalidationConflict()
    {
        return RevalidationFailure(
            "cv_review_conflict",
            "CV review data has changed. Refresh before retrying.");
    }

    // API-083: Append a new draft against approved criteria.
    // Preserve the original application and review history.
    public async Task<CvReviewRevalidationOperationResponse>
        RevalidateReviewAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewRevalidationRequest request,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await RevalidateReviewCoreAsync(
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
            return RevalidationConflict();
        }
        catch (Exception exception)
            when (IsDraftSqlConflict(exception))
        {
            _db.ChangeTracker.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            return RevalidationConflict();
        }
        catch (JsonException)
        {
            _db.ChangeTracker.Clear();

            return RevalidationFailure(
                "cv_review_json_invalid",
                "Stored CV review data is invalid.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return RevalidationFailure(
                "cv_review_database_error",
                "Unable to confirm the operation result. Refresh before retrying.");
        }
        catch (DbException)
        {
            _db.ChangeTracker.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            return RevalidationFailure(
                "cv_review_database_error",
                "Unable to confirm the operation result. Refresh before retrying.");
        }
        catch (InvalidOperationException)
        {
            _db.ChangeTracker.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            return RevalidationFailure(
                "cv_review_source_invalid",
                "CV review source data is invalid or unavailable.");
        }
    }

    private async Task<CvReviewRevalidationOperationResponse>
        RevalidateReviewCoreAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewRevalidationRequest request,
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
            return RevalidationFailure(
                "forbidden",
                "You are not authorized to revalidate CV reviews.");
        }

        if (applicationId == Guid.Empty ||
            request is null ||
            request.DocumentId == Guid.Empty ||
            request.DocumentVersion < 1 ||
            request.CurrentVacancyRevisionId == Guid.Empty ||
            request.TargetVacancyRevisionId == Guid.Empty ||
            request.ExpectedReviewVersionId == Guid.Empty ||
            request.ExpectedExtractionRunId == Guid.Empty)
        {
            return RevalidationFailure(
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
            return RevalidationFailure(
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
                return RevalidationFailure(
                    "invalid_cv_review_request",
                    "A valid extraction concurrency token is required.");
            }
        }
        else if (request.ExpectedExtractionRowVersion is not null)
        {
            return RevalidationFailure(
                "invalid_cv_review_request",
                "Extraction concurrency references are inconsistent.");
        }

        var workspace = await LoadWorkspaceAsync(
            applicationId,
            cancellationToken);

        if (workspace is null)
        {
            return RevalidationFailure(
                "application_not_found",
                "Application was not found.");
        }

        var application = workspace.Application;
        var document = workspace.Document;
        var previous = workspace.LatestReview;
        var extraction = workspace.LatestExtraction;

        if (!IsDraftStageAllowed(application.Stage))
        {
            return RevalidationFailure(
                "cv_review_stage_conflict",
                "The application stage does not permit CV revalidation.");
        }

        ValidateWorkspaceSources(workspace);

        if (application.RowVersion.Length != 8)
        {
            return RevalidationFailure(
                "cv_review_source_invalid",
                "Application concurrency data is invalid.");
        }

        if (!application.RowVersion.SequenceEqual(
                expectedApplicationVersion))
        {
            return RevalidationConflict();
        }

        if (previous is null)
        {
            return RevalidationFailure(
                "cv_review_draft_required",
                "An existing CV review is required for revalidation.");
        }

        if (previous.Id != request.ExpectedReviewVersionId ||
            !previous.RowVersion.SequenceEqual(
                expectedReviewVersion) ||
            extraction?.Id != request.ExpectedExtractionRunId ||
            (extraction is not null &&
             !extraction.RowVersion.SequenceEqual(
                 expectedExtractionVersion)))
        {
            return RevalidationConflict();
        }

        if (previous.Version == int.MaxValue)
        {
            return RevalidationFailure(
                "cv_review_source_invalid",
                "Review version data is invalid.");
        }

        if (document.Id != request.DocumentId ||
            document.Version != request.DocumentVersion ||
            workspace.ReviewRevision.Id !=
                request.CurrentVacancyRevisionId ||
            previous.VacancyRevisionId !=
                request.CurrentVacancyRevisionId)
        {
            return RevalidationConflict();
        }

        // Revalidate only against the current extraction.
        // A new extraction requires another draft review.
        if (previous.ExtractionRunId != extraction?.Id)
        {
            return RevalidationFailure(
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
            return RevalidationFailure(
                "cv_review_revision_not_approved",
                "The required vacancy revision is not approved.");
        }

        if (request.TargetVacancyRevisionId ==
                request.CurrentVacancyRevisionId)
        {
            return RevalidationFailure(
                "cv_review_revision_unchanged",
                "Select a different approved vacancy revision.");
        }

        var targetRevision = await _db.VacancyRevisions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.Id == request.TargetVacancyRevisionId &&
                    r.VacancyId == application.VacancyId,
                cancellationToken);

        if (targetRevision is null)
        {
            return RevalidationFailure(
                "cv_review_target_revision_invalid",
                "The target revision is not available for this vacancy.");
        }

        var targetDecisions = await _db
            .Set<VacancyDecision>()
            .AsNoTracking()
            .Where(d => d.VacancyRevisionId == targetRevision.Id)
            .ToListAsync(cancellationToken);

        if (!HasExactApproval(targetRevision, targetDecisions))
        {
            return RevalidationFailure(
                "cv_review_revision_not_approved",
                "The target vacancy revision is not approved.");
        }

        if (document.ScanStatus != CvScanStatuses.Clean ||
            !document.ScannedAtUtc.HasValue ||
            document.Sha256Hash.Length != 64 ||
            !document.Sha256Hash.All(Uri.IsHexDigit))
        {
            return RevalidationFailure(
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
                return RevalidationFailure(
                    "cv_review_extraction_pending",
                    "Wait for the current extraction to finish.");
            }

            // Reuse strict extraction JSON and evidence mapping.
            _ = MapExtraction(extraction);

            if (extraction.CompletedAtUtc is null ||
                previous.CreatedAtUtc <
                    extraction.CompletedAtUtc.Value)
            {
                return RevalidationFailure(
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
            .ValidateDraft(values);

        if (errors.Count != 0)
        {
            return RevalidationFailure(
                "invalid_cv_review_values",
                "Stored CV review fields are invalid.",
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

        // Reset duplicate decisions for explicit HR re-review.
        // Never automatically acknowledge a duplicate.
        var newResolution = new CvDuplicateResolutionValues();

        var changes = CvReviewChangeTracker.Compare(
            values,
            resolution,
            values,
            newResolution);

        var resolutionJson = JsonSerializer.Serialize(
            newResolution,
            DraftJsonOptions);

        var changedFieldsJson = JsonSerializer.Serialize(
            changes.ChangedFields,
            DraftJsonOptions);

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
                return RevalidationFailure(
                    "cv_review_source_invalid",
                    "CV review history is invalid.");
            }
        }

        // Verify actual private CV bytes before any persistence.
        if (!await VerifyReviewCvIntegrityAsync(
                document,
                cancellationToken))
        {
            return RevalidationFailure(
                "cv_review_document_not_available",
                "The submitted CV is unavailable or failed integrity verification.");
        }

        var now = DateTime.UtcNow;

        if (previous.CreatedAtUtc > now)
        {
            return RevalidationFailure(
                "cv_review_source_invalid",
                "CV review chronology is invalid.");
        }

        var revalidated = new CvReviewVersion
        {
            ApplicationId = application.Id,
            DocumentId = document.Id,
            VacancyRevisionId = targetRevision.Id,
            ExtractionRunId = previous.ExtractionRunId,
            ReviewerUserId = actorUserId,
            PreviousReviewVersionId = previous.Id,
            Version = previous.Version + 1,
            Status = CvReviewStatuses.Draft,

            // Preserve the exact reviewed values.
            ValuesJson = previous.ValuesJson,
            DuplicateResolutionJson = resolutionJson,
            ChangedFieldsJson = changedFieldsJson,

            CreatedAtUtc = now,
            ConfirmedAtUtc = null
        };

        _db.CandidateApplications.Attach(application);

        var entry = _db.Entry(application);

        entry.Property(a => a.RowVersion).OriginalValue =
            expectedApplicationVersion;

        application.UpdatedAtUtc = now;

        entry.Property(a => a.UpdatedAtUtc)
            .IsModified = true;

        // Do not change the application stage.
        // Do not modify any previous review version.
        _db.CvReviewVersions.Add(revalidated);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "CvReviewRevalidated",
                EntityType = "CvReviewVersion",
                EntityId = revalidated.Id,
                Outcome = "success"
            });
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                "CV revalidation audit event could not be staged.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (application.RowVersion.Length != 8 ||
            revalidated.RowVersion.Length != 8 ||
            application.RowVersion.SequenceEqual(
                expectedApplicationVersion))
        {
            throw new InvalidOperationException(
                "Revalidation concurrency token generation failed.");
        }

        var mappedReview = MapReview(revalidated)
            ?? throw new InvalidOperationException(
                "Revalidation response mapping failed.");

        var response = new CvReviewRevalidationResponse
        {
            ApplicationId = application.Id,
            DocumentId = document.Id,
            ApplicationVacancyRevisionId =
                application.VacancyRevisionId,

            PreviousReviewVacancyRevisionId =
                previous.VacancyRevisionId,

            ReviewVacancyRevisionId =
                revalidated.VacancyRevisionId,

            ConfirmationInvalidated =
                previous.Status == CvReviewStatuses.Confirmed,

            RequiresConfirmation = true,
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

        return new CvReviewRevalidationOperationResponse
        {
            Success = true,
            Code = "cv_review_revalidated",
            Message = "CV review revalidation draft created successfully.",
            Data = response
        };
    }
}
