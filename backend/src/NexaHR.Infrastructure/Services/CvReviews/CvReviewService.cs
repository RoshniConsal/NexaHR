using System.Text.Json;
using NexaHR.Application.Validation.CvReviews;
using NexaHR.Infrastructure.Services.Applications;
using System.Data.Common;
using System.Data;
using NexaHR.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.Interfaces.CvReviews;
using NexaHR.Application.Interfaces.Audit;
using NexaHR.Application.Interfaces.CvDocuments;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.CvReviews;

public sealed partial class CvReviewService : ICvReviewService
{
    private readonly NexaHRDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICvFileStorage _storage;
    private readonly IAuditWriter _audit;

    public CvReviewService(
        NexaHRDbContext db,
        UserManager<ApplicationUser> users,
        ICvFileStorage storage,
        IAuditWriter audit)
    {
        _db = db;
        _users = users;
        _storage = storage;
        _audit = audit;
    }

    private static CvReviewWorkspaceOperationResponse Failure(
        string code,
        string message)
    {
        return new CvReviewWorkspaceOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    private async Task<bool> IsAuthorizedHrAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                u => u.Id == actorUserId,
                cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            await _users.IsLockedOutAsync(user))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var roles = await _users.GetRolesAsync(user);

        cancellationToken.ThrowIfCancellationRequested();

        return roles.Count == 1 &&
            string.Equals(
                roles[0],
                AppRoles.HRManager,
                StringComparison.Ordinal);
    }

    // API-079: HR-only CV review workspace.
    public async Task<CvReviewWorkspaceOperationResponse>
        GetWorkspaceAsync(
            Guid actorUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            if (!await IsAuthorizedHrAsync(
                actorUserId,
                cancellationToken))
            {
                return Failure(
                    "forbidden",
                    "You are not authorized to access CV reviews.");
            }

            if (applicationId == Guid.Empty)
            {
                return Failure(
                    "invalid_application",
                    "A valid application ID is required.");
            }

            var workspace = await LoadWorkspaceAsync(
                applicationId,
                cancellationToken);

            if (workspace is null)
            {
                return Failure(
                    "application_not_found",
                    "Application was not found.");
            }

            if (!HasExactApproval(
                    workspace.ApplicationRevision,
                    workspace.RevisionDecisions) ||
                !HasExactApproval(
                    workspace.ReviewRevision,
                    workspace.RevisionDecisions))
            {
                return Failure(
                    "cv_review_revision_not_approved",
                    "The required vacancy revision is not approved.");
            }

            if (workspace.Document.ScanStatus != CvScanStatuses.Clean ||
                !workspace.Document.ScannedAtUtc.HasValue)
            {
                return Failure(
                    "cv_review_document_not_available",
                    "The submitted CV is not available for review.");
            }

            if (workspace.Application.RowVersion.Length != 8 ||
                (workspace.LatestReview is not null &&
                 workspace.LatestReview.RowVersion.Length != 8) ||
                (workspace.LatestExtraction is not null &&
                 workspace.LatestExtraction.RowVersion.Length != 8))
            {
                return Failure(
                    "cv_review_source_invalid",
                    "CV review source data is invalid.");
            }

            ValidateWorkspaceSources(workspace);

            var response = MapWorkspace(workspace);

            if (workspace.LatestReview is not null)
            {
                var reviewedValues =
                    CvReviewJsonReader.ReadValues(
                        workspace.LatestReview.ValuesJson);

                var duplicates =
                    await CandidateContactDuplicateChecker.CheckAsync(
                        _db,
                        _users,
                        workspace.Application.CandidateUserId,
                        reviewedValues.Email,
                        reviewedValues.PhoneNumber,
                        cancellationToken);

                response.DuplicateFlags =
                    new CvReviewDuplicateFlagsResponse
                    {
                        DuplicateEmailFlag = duplicates.EmailMatch,
                        DuplicatePhoneFlag = duplicates.PhoneMatch,
                        Source = "CurrentReview"
                    };
            }

            cancellationToken.ThrowIfCancellationRequested();

            await transaction.CommitAsync(cancellationToken);

            return new CvReviewWorkspaceOperationResponse
            {
                Success = true,
                Code = "cv_review_workspace_retrieved",
                Message = "CV review workspace retrieved successfully.",
                Data = response
            };
        }
        catch (JsonException)
        {
            return Failure(
                "cv_review_json_invalid",
                "Stored CV review data is invalid.");
        }
        catch (DbException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Failure(
                "cv_review_database_error",
                "CV review data is temporarily unavailable.");
        }
        catch (InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Failure(
                "cv_review_source_invalid",
                "CV review source data is unavailable or invalid.");
        }
    }

    private sealed record WorkspaceData(
        CandidateApplication Application,
        CandidateProfile Profile,
        ApplicationUser Candidate,
        CvDocument Document,
        VacancyRevision ApplicationRevision,
        VacancyRevision ReviewRevision,
        IReadOnlyList<VacancyDecision> RevisionDecisions,
        CvExtractionRun? LatestExtraction,
        CvReviewVersion? LatestReview);

    private async Task<WorkspaceData?> LoadWorkspaceAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var application = await _db.CandidateApplications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == applicationId,
                cancellationToken);

        if (application is null)
        {
            return null;
        }

        var profile = await _db.CandidateProfiles
            .AsNoTracking()
            .SingleAsync(
                p => p.UserId == application.CandidateUserId,
                cancellationToken);

        var candidate = await _db.Users
            .AsNoTracking()
            .SingleAsync(
                u => u.Id == application.CandidateUserId,
                cancellationToken);

        var document = await _db.CvDocuments
            .AsNoTracking()
            .SingleAsync(
                d => d.Id == application.DocumentId &&
                    d.OwnerUserId == application.CandidateUserId,
                cancellationToken);

        var applicationRevision = await _db.VacancyRevisions
            .AsNoTracking()
            .SingleAsync(
                r => r.Id == application.VacancyRevisionId &&
                    r.VacancyId == application.VacancyId,
                cancellationToken);

        var latestExtraction = await _db.CvExtractionRuns
            .AsNoTracking()
            .Where(
                e => e.ApplicationId == application.Id &&
                    e.DocumentId == application.DocumentId)
            .OrderByDescending(e => e.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var latestReview = await _db.CvReviewVersions
            .AsNoTracking()
            .Where(
                r => r.ApplicationId == application.Id &&
                    r.DocumentId == application.DocumentId)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var reviewRevisionId =
            latestReview?.VacancyRevisionId ??
            application.VacancyRevisionId;

        var reviewRevision = reviewRevisionId ==
            applicationRevision.Id
            ? applicationRevision
            : await _db.VacancyRevisions
                .AsNoTracking()
                .SingleAsync(
                    r => r.Id == reviewRevisionId &&
                        r.VacancyId == application.VacancyId,
                    cancellationToken);

        var revisionDecisions = await _db
            .Set<VacancyDecision>()
            .AsNoTracking()
            .Where(d =>
                d.VacancyRevisionId == reviewRevisionId ||
                d.VacancyRevisionId == application.VacancyRevisionId)
            .ToListAsync(cancellationToken);

        return new WorkspaceData(
            application,
            profile,
            candidate,
            document,
            applicationRevision,
            reviewRevision,
            revisionDecisions,
            latestExtraction,
            latestReview);
    }

    private static bool HasExactApproval(
        VacancyRevision revision,
        IReadOnlyList<VacancyDecision> decisions)
    {
        if (!revision.SubmittedAtUtc.HasValue ||
            !revision.SubmittedByUserId.HasValue)
        {
            return false;
        }

        var matches = decisions
            .Where(d => d.VacancyRevisionId == revision.Id)
            .ToList();

        return matches.Count == 1 &&
            matches[0].ApproverRole == AppRoles.CEO &&
            matches[0].Decision == VacancyDecisionTypes.Approved &&
            matches[0].AtUtc >= revision.SubmittedAtUtc.Value;
    }

    private static DateTime Utc(DateTime value)
    {
        return DateTime.SpecifyKind(
            value,
            DateTimeKind.Utc);
    }

    private static DateTime? Utc(DateTime? value)
    {
        return value.HasValue
            ? Utc(value.Value)
            : null;
    }

    private static JsonElement ReadJson(
        string json,
        JsonValueKind expectedKind)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException(
                "Stored JSON is empty.");
        }

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != expectedKind)
        {
            throw new JsonException(
                "Unexpected JSON root type.");
        }

        return document.RootElement.Clone();
    }

    private static JsonElement? ReadOptionalJson(
        string? json,
        JsonValueKind expectedKind)
    {
        return json is null
            ? null
            : ReadJson(json, expectedKind);
    }

    private static CvReviewExtractionResponse? MapExtraction(
        CvExtractionRun? extraction)
    {
        if (extraction is null)
        {
            return null;
        }

        CvStructuredValues? values = null;

        IReadOnlyList<CvFieldEvidence>? evidence = null;

        if (extraction.ResultJson is not null)
        {
            values = CvReviewJsonReader.ReadValues(
                extraction.ResultJson);
        }

        if (extraction.EvidenceJson is not null)
        {
            evidence = CvReviewJsonReader.ReadEvidence(
                extraction.EvidenceJson);
        }

        if (extraction.Status == CvExtractionStatuses.ReadyForReview)
        {
            if (values is null ||
                CvReviewMetadataValidator
                    .ValidateEvidence(values, evidence).Count != 0)
            {
                throw new JsonException(
                    "Stored extraction values are invalid.");
            }
        }
        else if (values is not null || evidence is not null)
        {
            throw new JsonException(
                "Unexpected extraction values.");
        }

        return new CvReviewExtractionResponse
        {
            ExtractionRunId = extraction.Id,
            RowVersion = Convert.ToBase64String(
                extraction.RowVersion),
            DocumentId = extraction.DocumentId,
            Version = extraction.Version,
            ParserVersion = extraction.ParserVersion,
            Status = extraction.Status,
            CreatedAtUtc = Utc(extraction.CreatedAtUtc),
            StartedAtUtc = Utc(extraction.StartedAtUtc),
            CompletedAtUtc = Utc(extraction.CompletedAtUtc),

            // Never expose raw parser or exception diagnostics.
            FailureCode =
                extraction.Status == CvExtractionStatuses.Failed
                    ? "extraction_failed"
                    : null,

            ExtractedValues = values is null
                ? null
                : JsonSerializer.SerializeToElement(
                    values, DraftJsonOptions),

            Evidence = evidence is null
                ? null
                : JsonSerializer.SerializeToElement(
                    evidence, DraftJsonOptions)
        };
    }

    private static CvReviewVersionResponse? MapReview(
        CvReviewVersion? review)
    {
        if (review is null)
        {
            return null;
        }

        var values = CvReviewJsonReader.ReadValues(
            review.ValuesJson);

        var resolution =
            CvReviewJsonReader.ReadDuplicateResolution(
                review.DuplicateResolutionJson);

        var changedFields =
            CvReviewJsonReader.ReadChangedFields(
                review.ChangedFieldsJson);

        if (review.Version < 1 ||
            review.Status is not
                (CvReviewStatuses.Draft or
                 CvReviewStatuses.Confirmed))
        {
            throw new InvalidOperationException(
                "Stored review metadata is invalid.");
        }

        var valueErrors =
            review.Status == CvReviewStatuses.Confirmed
                ? CvStructuredValuesValidator
                    .ValidateConfirmation(values)
                : CvStructuredValuesValidator
                    .ValidateDraft(values);

        if (valueErrors.Count != 0 ||
            CvReviewMetadataValidator
                .ValidateDuplicateDraft(resolution).Count != 0 ||
            CvReviewMetadataValidator
                .ValidateChangedFields(changedFields).Count != 0)
        {
            throw new JsonException(
                "Stored review values are invalid.");
        }

        return new CvReviewVersionResponse
        {
            ReviewVersionId = review.Id,
            DocumentId = review.DocumentId,
            VacancyRevisionId = review.VacancyRevisionId,
            ExtractionRunId = review.ExtractionRunId,
            PreviousReviewVersionId =
                review.PreviousReviewVersionId,
            ReviewerUserId = review.ReviewerUserId,
            Version = review.Version,
            Status = review.Status,

            ReviewedValues =
                JsonSerializer.SerializeToElement(
                    values, DraftJsonOptions),

            DuplicateResolution =
                JsonSerializer.SerializeToElement(
                    resolution, DraftJsonOptions),

            ChangedFields =
                JsonSerializer.SerializeToElement(
                    changedFields, DraftJsonOptions),

            CreatedAtUtc = Utc(review.CreatedAtUtc),
            ConfirmedAtUtc = Utc(review.ConfirmedAtUtc),
            RowVersion = Convert.ToBase64String(
                review.RowVersion)
        };
    }

    private static void ValidateWorkspaceSources(
        WorkspaceData workspace)
    {
        var application = workspace.Application;
        var document = workspace.Document;
        var extraction = workspace.LatestExtraction;
        var review = workspace.LatestReview;

        if (extraction is not null)
        {
            if (extraction.ApplicationId != application.Id ||
                extraction.DocumentId != document.Id ||
                extraction.Version < 1 ||
                extraction.RowVersion.Length != 8 ||
                !string.Equals(
                    extraction.SourceSha256Hash,
                    document.Sha256Hash,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(
                    extraction.ParserVersion))
            {
                throw new InvalidOperationException(
                    "Extraction source is invalid.");
            }

            var started = extraction.StartedAtUtc;
            var completed = extraction.CompletedAtUtc;

            if ((started.HasValue &&
                 started.Value < extraction.CreatedAtUtc) ||
                (completed.HasValue &&
                 (!started.HasValue ||
                  completed.Value < started.Value)))
            {
                throw new InvalidOperationException(
                    "Extraction timestamps are invalid.");
            }

            var lifecycleValid = extraction.Status switch
            {
                CvExtractionStatuses.Queued =>
                    started is null &&
                    completed is null &&
                    extraction.ResultJson is null &&
                    extraction.EvidenceJson is null &&
                    extraction.FailureCode is null,

                CvExtractionStatuses.Processing =>
                    started.HasValue &&
                    completed is null &&
                    extraction.ResultJson is null &&
                    extraction.EvidenceJson is null &&
                    extraction.FailureCode is null,

                CvExtractionStatuses.ReadyForReview =>
                    started.HasValue &&
                    completed.HasValue &&
                    extraction.ResultJson is not null &&
                    extraction.FailureCode is null,

                CvExtractionStatuses.Failed =>
                    started.HasValue &&
                    completed.HasValue &&
                    extraction.ResultJson is null &&
                    extraction.EvidenceJson is null &&
                    !string.IsNullOrWhiteSpace(
                        extraction.FailureCode),

                _ => false
            };

            if (!lifecycleValid)
            {
                throw new InvalidOperationException(
                    "Extraction lifecycle is invalid.");
            }
        }

        if (review is not null)
        {
            if (review.ApplicationId != application.Id ||
                review.DocumentId != document.Id ||
                review.VacancyRevisionId !=
                    workspace.ReviewRevision.Id ||
                review.RowVersion.Length != 8 ||
                review.Version < 1 ||
                review.ReviewerUserId == Guid.Empty ||
                (review.Version == 1 &&
                 review.PreviousReviewVersionId.HasValue) ||
                (review.Version > 1 &&
                 !review.PreviousReviewVersionId.HasValue) ||
                review.PreviousReviewVersionId == review.Id)
            {
                throw new InvalidOperationException(
                    "Review source is invalid.");
            }

            var statusValid = review.Status switch
            {
                CvReviewStatuses.Draft =>
                    review.ConfirmedAtUtc is null,

                CvReviewStatuses.Confirmed =>
                    review.ConfirmedAtUtc.HasValue &&
                    review.ConfirmedAtUtc.Value >=
                        review.CreatedAtUtc,

                _ => false
            };

            if (!statusValid)
            {
                throw new InvalidOperationException(
                    "Review lifecycle is invalid.");
            }
        }
    }

    private static CvReviewWorkspaceResponse MapWorkspace(
        WorkspaceData workspace)
    {
        var application = workspace.Application;
        var document = workspace.Document;
        var candidate = workspace.Candidate;
        var profile = workspace.Profile;

        return new CvReviewWorkspaceResponse
        {
            ApplicationId = application.Id,
            VacancyId = application.VacancyId,

            ApplicationVacancyRevisionId =
                workspace.ApplicationRevision.Id,

            ReviewVacancyRevisionId =
                workspace.ReviewRevision.Id,

            JobTitle = workspace.ApplicationRevision.Title,
            Stage = application.Stage,
            AppliedAtUtc = Utc(application.AppliedAtUtc),

            ApplicationRowVersion =
                Convert.ToBase64String(
                    application.RowVersion),

            Document = new CvReviewDocumentResponse
            {
                DocumentId = document.Id,
                Version = document.Version,
                OriginalFileName = document.OriginalFileName,
                ContentType = document.ContentType,
                SizeBytes = document.SizeBytes,
                ScanStatus = document.ScanStatus,
                UploadedAtUtc = Utc(document.CreatedAtUtc),

                // Protected HR content route is not enabled yet.
                ContentUrl = null,
                CanAccessContent = false
            },

            ProfileDefaults = new CvReviewProfileDefaultsResponse
            {
                CandidateUserId = candidate.Id,
                FullName = candidate.FullName,
                Email = candidate.Email,
                PhoneNumber = candidate.PhoneNumber,
                Skills = profile.Skills,
                Education = profile.Education,
                Source = "CandidateProfile"
            },

            DuplicateFlags = new CvReviewDuplicateFlagsResponse
            {
                DuplicateEmailFlag =
                    application.DuplicateEmailFlag,

                DuplicatePhoneFlag =
                    application.DuplicatePhoneFlag,

                Source = "ApplicationSubmission"
            },

            LatestExtraction = MapExtraction(
                workspace.LatestExtraction),

            LatestReview = MapReview(
                workspace.LatestReview)
        };
    }
}
