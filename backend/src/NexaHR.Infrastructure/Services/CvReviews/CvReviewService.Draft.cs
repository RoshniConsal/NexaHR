using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.DTOs.Audit;
using NexaHR.Application.Validation.CvReviews;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Services.Applications;

namespace NexaHR.Infrastructure.Services.CvReviews;

public sealed partial class CvReviewService
{
    private static readonly JsonSerializerOptions DraftJsonOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    private static CvReviewDraftOperationResponse DraftFailure(
        string code,
        string message,
        IReadOnlyList<CvFieldValidationError>? errors = null)
    {
        return new CvReviewDraftOperationResponse
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors ?? []
        };
    }

    private static bool TryReadDraftRowVersion(
        string? value,
        out byte[] bytes)
    {
        bytes = [];

        if (value is null || value.Length != 12)
        {
            return false;
        }

        var buffer = new byte[8];

        if (!Convert.TryFromBase64String(
                value, buffer, out var written) ||
            written != 8 ||
            !string.Equals(
                Convert.ToBase64String(buffer),
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        bytes = buffer;
        return true;
    }

    private static bool IsDraftStageAllowed(string stage)
    {
        return stage == ApplicationStatuses.Applied ||
            stage == ApplicationStatuses.UnderReview;
    }

    private static bool IsDraftSqlConflict(Exception exception)
    {
        for (Exception? current = exception;
            current is not null;
            current = current.InnerException)
        {
            if (current is SqlException sql &&
                sql.Number is 1205 or 2601 or 2627)
            {
                return true;
            }
        }

        return false;
    }

    private static CvReviewDraftOperationResponse DraftConflict()
    {
        return DraftFailure(
            "cv_review_conflict",
            "CV review data has changed. Refresh before retrying.");
    }

    // API-080: Save draft without advancing application stage.
    public async Task<CvReviewDraftOperationResponse> SaveDraftAsync(
        Guid actorUserId,
        Guid applicationId,
        CvReviewDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await SaveDraftCoreAsync(
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
            return DraftConflict();
        }
        catch (Exception exception)
            when (IsDraftSqlConflict(exception))
        {
            _db.ChangeTracker.Clear();
            cancellationToken.ThrowIfCancellationRequested();
            return DraftConflict();
        }
        catch (JsonException)
        {
            _db.ChangeTracker.Clear();

            return DraftFailure(
                "cv_review_json_invalid",
                "Stored CV review data is invalid.");
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            return DraftFailure(
                "cv_review_database_error",
                "Unable to confirm the draft save result. Refresh before retrying.");
        }
        catch (DbException)
        {
            _db.ChangeTracker.Clear();
            cancellationToken.ThrowIfCancellationRequested();

            return DraftFailure(
                "cv_review_database_error",
                "Unable to confirm the draft save result. Refresh before retrying.");
        }
        catch (InvalidOperationException)
        {
            _db.ChangeTracker.Clear();
            cancellationToken.ThrowIfCancellationRequested();

            return DraftFailure(
                "cv_review_source_invalid",
                "CV review source data is invalid or unavailable.");
        }
    }

    private async Task<CvReviewDraftOperationResponse>
        SaveDraftCoreAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewDraftRequest request,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Authorize before disclosing application existence.
        if (!await IsAuthorizedHrAsync(
                actorUserId, cancellationToken))
        {
            return DraftFailure(
                "forbidden",
                "You are not authorized to save CV reviews.");
        }

        if (applicationId == Guid.Empty ||
            request is null ||
            request.DocumentId == Guid.Empty ||
            request.DocumentVersion < 1 ||
            request.VacancyRevisionId == Guid.Empty ||
            request.ExpectedReviewVersionId == Guid.Empty ||
            request.ExpectedExtractionRunId == Guid.Empty)
        {
            return DraftFailure(
                "invalid_cv_review_request",
                "Valid application and source references are required.");
        }

        if (!TryReadDraftRowVersion(
                request.ApplicationRowVersion,
                out var expectedApplicationVersion))
        {
            return DraftFailure(
                "invalid_cv_review_request",
                "A valid application concurrency token is required.");
        }

        byte[] expectedReviewVersion = [];

        if (request.ExpectedReviewVersionId.HasValue)
        {
            if (!TryReadDraftRowVersion(
                    request.ExpectedReviewRowVersion,
                    out expectedReviewVersion))
            {
                return DraftFailure(
                    "invalid_cv_review_request",
                    "A valid review concurrency token is required.");
            }
        }
        else if (request.ExpectedReviewRowVersion is not null)
        {
            return DraftFailure(
                "invalid_cv_review_request",
                "Review concurrency references are inconsistent.");
        }

        byte[] expectedExtractionVersion = [];

        if (request.ExpectedExtractionRunId.HasValue)
        {
            if (!TryReadDraftRowVersion(
                    request.ExpectedExtractionRowVersion,
                    out expectedExtractionVersion))
            {
                return DraftFailure(
                    "invalid_cv_review_request",
                    "A valid extraction concurrency token is required.");
            }
        }
        else if (request.ExpectedExtractionRowVersion is not null)
        {
            return DraftFailure(
                "invalid_cv_review_request",
                "Extraction concurrency references are inconsistent.");
        }

        var workspace = await LoadWorkspaceAsync(
            applicationId, cancellationToken);

        if (workspace is null)
        {
            return DraftFailure(
                "application_not_found",
                "Application was not found.");
        }

        var application = workspace.Application;
        var document = workspace.Document;
        var previous = workspace.LatestReview;
        var extraction = workspace.LatestExtraction;

        if (!IsDraftStageAllowed(application.Stage))
        {
            return DraftFailure(
                "cv_review_stage_conflict",
                "The application stage does not permit draft editing.");
        }

        if (application.RowVersion.Length != 8 ||
            (previous is not null &&
             previous.RowVersion.Length != 8) ||
            (extraction is not null &&
             extraction.RowVersion.Length != 8))
        {
            return DraftFailure(
                "cv_review_source_invalid",
                "CV review concurrency data is invalid.");
        }

        if (!application.RowVersion.SequenceEqual(
                expectedApplicationVersion) ||
            previous?.Id != request.ExpectedReviewVersionId ||
            (previous is not null &&
             !previous.RowVersion.SequenceEqual(expectedReviewVersion)) ||
            extraction?.Id != request.ExpectedExtractionRunId ||
            (extraction is not null &&
             !extraction.RowVersion.SequenceEqual(
                 expectedExtractionVersion)))
        {
            return DraftConflict();
        }

        // Draft editing must not silently replace pinned sources.
        if (document.Id != request.DocumentId ||
            document.Version != request.DocumentVersion ||
            workspace.ReviewRevision.Id != request.VacancyRevisionId)
        {
            return DraftConflict();
        }

        if (!HasExactApproval(
                workspace.ApplicationRevision,
                workspace.RevisionDecisions) ||
            !HasExactApproval(
                workspace.ReviewRevision,
                workspace.RevisionDecisions))
        {
            return DraftFailure(
                "cv_review_revision_not_approved",
                "The required vacancy revision is not approved.");
        }

        if (document.ScanStatus != CvScanStatuses.Clean ||
            !document.ScannedAtUtc.HasValue ||
            document.Sha256Hash.Length != 64 ||
            !document.Sha256Hash.All(Uri.IsHexDigit))
        {
            return DraftFailure(
                "cv_review_document_not_available",
                "The submitted CV is not available for review.");
        }

        if (extraction is not null)
        {
            if (extraction.ApplicationId != application.Id ||
                extraction.DocumentId != document.Id ||
                !string.Equals(
                    extraction.SourceSha256Hash,
                    document.Sha256Hash,
                    StringComparison.Ordinal))
            {
                return DraftFailure(
                    "cv_review_source_invalid",
                    "CV extraction source data is invalid.");
            }

            // Permit manual fallback after extraction failure.
            // Do not save against a still-changing extraction.
            if (extraction.Status !=
                    CvExtractionStatuses.ReadyForReview &&
                extraction.Status != CvExtractionStatuses.Failed)
            {
                return DraftFailure(
                    "cv_review_extraction_pending",
                    "Wait for the current extraction to finish.");
            }

            if (!extraction.StartedAtUtc.HasValue ||
                !extraction.CompletedAtUtc.HasValue ||
                extraction.StartedAtUtc.Value <
                    extraction.CreatedAtUtc ||
                extraction.CompletedAtUtc.Value <
                    extraction.StartedAtUtc.Value)
            {
                return DraftFailure(
                    "cv_review_source_invalid",
                    "CV extraction lifecycle data is invalid.");
            }
        }

        CvStructuredValues values;
        CvDuplicateResolutionValues resolution;

        try
        {
            if (request.Values.ValueKind != JsonValueKind.Object ||
                request.DuplicateResolution.ValueKind !=
                    JsonValueKind.Object)
            {
                return DraftFailure(
                    "invalid_cv_review_values",
                    "Structured review values and duplicate resolution are required.");
            }

            values = CvReviewJsonReader.ReadValues(
                request.Values.GetRawText());

            resolution =
                CvReviewJsonReader.ReadDuplicateResolution(
                    request.DuplicateResolution.GetRawText());
        }
        catch (JsonException)
        {
            return DraftFailure(
                "invalid_cv_review_values",
                "The submitted CV review JSON is invalid.");
        }

        var errors = CvStructuredValuesValidator
            .ValidateDraft(values)
            .Concat(CvReviewMetadataValidator
                .ValidateDuplicateDraft(resolution))
            .ToList();

        if (errors.Count != 0)
        {
            return DraftFailure(
                "invalid_cv_review_values",
                "Correct the invalid CV review fields.",
                errors.AsReadOnly());
        }

        // Recalculate duplicate flags using corrected contacts.
        var duplicates =
            await CandidateContactDuplicateChecker.CheckAsync(
                _db,
                _users,
                application.CandidateUserId,
                values.Email,
                values.PhoneNumber,
                cancellationToken);

        CvStructuredValues? previousValues = null;
        CvDuplicateResolutionValues? previousResolution = null;

        if (previous is not null)
        {
            previousValues = CvReviewJsonReader.ReadValues(
                previous.ValuesJson);

            previousResolution =
                CvReviewJsonReader.ReadDuplicateResolution(
                    previous.DuplicateResolutionJson);

            var previousFields =
                CvReviewJsonReader.ReadChangedFields(
                    previous.ChangedFieldsJson);

            if (CvReviewMetadataValidator
                    .ValidateChangedFields(previousFields).Count != 0 ||
                previous.Version < 1 ||
                previous.Version == int.MaxValue ||
                (previous.Status != CvReviewStatuses.Draft &&
                 previous.Status != CvReviewStatuses.Confirmed))
            {
                return DraftFailure(
                    "cv_review_source_invalid",
                    "Previous CV review data is invalid.");
            }
        }

        var changes = CvReviewChangeTracker.Compare(
            previousValues,
            previousResolution,
            values,
            resolution);

        var sourceChanged =
            previous is not null &&
            previous.ExtractionRunId != extraction?.Id;

        var hasMaterialChanges =
            changes.HasMaterialChanges || sourceChanged;

        var confirmationInvalidated =
            previous?.Status == CvReviewStatuses.Confirmed;

        // Even an identical Save Draft must not silently
        // replace an existing confirmed review.
        if (confirmationInvalidated && !hasMaterialChanges)
        {
            return DraftFailure(
                "cv_review_no_changes",
                "The current confirmed review has no material changes.");
        }

        // Prevent accidental submission of an old duplicate
        // acknowledgement after the corresponding contact changes.
        if (previousResolution is not null &&
            previousValues is not null)
        {
            var staleEmailAcknowledgement =
                !string.Equals(
                    previousValues.Email,
                    values.Email,
                    StringComparison.Ordinal) &&
                resolution.EmailResolution == "Acknowledged" &&
                previousResolution.EmailResolution == "Acknowledged" &&
                string.Equals(
                    resolution.EmailReason,
                    previousResolution.EmailReason,
                    StringComparison.Ordinal);

            var stalePhoneAcknowledgement =
                !string.Equals(
                    previousValues.PhoneNumber,
                    values.PhoneNumber,
                    StringComparison.Ordinal) &&
                resolution.PhoneResolution == "Acknowledged" &&
                previousResolution.PhoneResolution == "Acknowledged" &&
                string.Equals(
                    resolution.PhoneReason,
                    previousResolution.PhoneReason,
                    StringComparison.Ordinal);

            if (staleEmailAcknowledgement ||
                stalePhoneAcknowledgement)
            {
                return DraftFailure(
                    "cv_review_duplicate_resolution_stale",
                    "Review the duplicate acknowledgement after changing contact details.");
            }
        }

        // Verify the actual private CV content before
        // staging any draft persistence changes.
        if (!await VerifyReviewCvIntegrityAsync(
                document, cancellationToken))
        {
            return DraftFailure(
                "cv_review_document_not_available",
                "The submitted CV is unavailable or failed integrity verification.");
        }

        var now = DateTime.UtcNow;

        var draft = new CvReviewVersion
        {
            ApplicationId = application.Id,
            DocumentId = document.Id,
            VacancyRevisionId = workspace.ReviewRevision.Id,
            ExtractionRunId = extraction?.Id,
            ReviewerUserId = actorUserId,
            PreviousReviewVersionId = previous?.Id,
            Version = (previous?.Version ?? 0) + 1,
            Status = CvReviewStatuses.Draft,
            ValuesJson = JsonSerializer.Serialize(
                values, DraftJsonOptions),
            DuplicateResolutionJson = JsonSerializer.Serialize(
                resolution, DraftJsonOptions),
            ChangedFieldsJson = JsonSerializer.Serialize(
                changes.ChangedFields, DraftJsonOptions),
            CreatedAtUtc = now,
            ConfirmedAtUtc = null
        };

        // Ensure stored canonical JSON remains within reader limits.
        _ = CvReviewJsonReader.ReadValues(draft.ValuesJson);

        _ = CvReviewJsonReader.ReadDuplicateResolution(
            draft.DuplicateResolutionJson);

        _ = CvReviewJsonReader.ReadChangedFields(
            draft.ChangedFieldsJson);

        // Attach only the detached application record.
        // Preserve its immutable submission references and flags.
        _db.CandidateApplications.Attach(application);

        var entry = _db.Entry(application);

        entry.Property(a => a.RowVersion).OriginalValue =
            expectedApplicationVersion;

        application.UpdatedAtUtc = now;

        // Force an application rowversion update.
        entry.Property(a => a.UpdatedAtUtc).IsModified = true;

        // Append a new review; never modify previous versions.
        _db.CvReviewVersions.Add(draft);

        // Persist draft, application update and audit atomically.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _audit.StageEvent(new AuditWriteRequest
            {
                ActorUserId = actorUserId,
                ActorRole = AppRoles.HRManager,
                Module = "Recruitment",
                Action = "CvReviewDraftSaved",
                EntityType = "CvReviewVersion",
                EntityId = draft.Id,
                Outcome = "success"
            });
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                "CV review audit event could not be staged.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (application.RowVersion.Length != 8 ||
            draft.RowVersion.Length != 8 ||
            application.RowVersion.SequenceEqual(
                expectedApplicationVersion))
        {
            throw new InvalidOperationException(
                "Draft concurrency token generation failed.");
        }

        var mappedReview = MapReview(draft)
            ?? throw new InvalidOperationException(
                "Draft response mapping failed.");

        var response = new CvReviewDraftResponse
        {
            ApplicationId = application.Id,
            DocumentId = document.Id,
            VacancyRevisionId = draft.VacancyRevisionId,
            ApplicationRowVersion =
                Convert.ToBase64String(application.RowVersion),
            Review = mappedReview,
            DuplicateFlags = new CvReviewDuplicateFlagsResponse
            {
                DuplicateEmailFlag = duplicates.EmailMatch,
                DuplicatePhoneFlag = duplicates.PhoneMatch,
                Source = "CurrentReview"
            },
            HasMaterialChanges = hasMaterialChanges,
            ConfirmationInvalidated = confirmationInvalidated
        };

        cancellationToken.ThrowIfCancellationRequested();

        await transaction.CommitAsync(cancellationToken);

        return new CvReviewDraftOperationResponse
        {
            Success = true,
            Code = "cv_review_draft_saved",
            Message = "CV review draft saved successfully.",
            Data = response
        };
    }
}
