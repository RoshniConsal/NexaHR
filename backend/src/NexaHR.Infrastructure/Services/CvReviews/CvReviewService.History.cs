using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaHR.Application.DTOs.CvReviews;
using NexaHR.Application.Validation.CvReviews;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;

namespace NexaHR.Infrastructure.Services.CvReviews;

public sealed partial class CvReviewService
{
    private static CvReviewHistoryOperationResponse
        HistoryFailure(string code, string message)
    {
        return new CvReviewHistoryOperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }

    // API-082: HR-only immutable review history.
    public async Task<CvReviewHistoryOperationResponse>
        GetReviewHistoryAsync(
            Guid actorUserId,
            Guid applicationId,
            CvReviewHistoryQuery query,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Authorization precedes application lookup.
            if (!await IsAuthorizedHrAsync(
                    actorUserId,
                    cancellationToken))
            {
                return HistoryFailure(
                    "forbidden",
                    "You are not authorized to access CV review history.");
            }

            if (applicationId == Guid.Empty)
            {
                return HistoryFailure(
                    "invalid_application",
                    "A valid application ID is required.");
            }

            if (query is null ||
                query.Page < 1 ||
                query.Page > 100000 ||
                query.PageSize < 1 ||
                query.PageSize > 100)
            {
                return HistoryFailure(
                    "invalid_cv_review_history_query",
                    "Invalid history pagination.");
            }

            var workspace = await LoadWorkspaceAsync(
                applicationId,
                cancellationToken);

            if (workspace is null)
            {
                return HistoryFailure(
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
                return HistoryFailure(
                    "cv_review_revision_not_approved",
                    "The required vacancy revision is not approved.");
            }

            if (workspace.Document.ScanStatus !=
                    CvScanStatuses.Clean ||
                !workspace.Document.ScannedAtUtc.HasValue)
            {
                return HistoryFailure(
                    "cv_review_document_not_available",
                    "The submitted CV is unavailable.");
            }

            if (workspace.Application.RowVersion.Length != 8)
            {
                throw new InvalidOperationException(
                    "Application concurrency data is invalid.");
            }

            ValidateWorkspaceSources(workspace);

            var historyQuery = _db.CvReviewVersions
                .AsNoTracking()
                .Where(r =>
                    r.ApplicationId == applicationId &&
                    r.DocumentId == workspace.Document.Id);

            var totalCount = await historyQuery.CountAsync(
                cancellationToken);

            var latest = workspace.LatestReview;

            // Review versions must be contiguous from version 1.
            if ((totalCount == 0 && latest is not null) ||
                (totalCount > 0 &&
                 (latest is null ||
                  latest.Version != totalCount)))
            {
                throw new InvalidOperationException(
                    "Review history sequence is invalid.");
            }

            var skip = checked(
                (query.Page - 1) * query.PageSize);

            var reviews = await historyQuery
                .OrderByDescending(r => r.Version)
                .Skip(skip)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            // Fetch one additional predecessor for the
            // boundary of this page, when necessary.
            var previousIds = reviews
                .Where(r =>
                    r.PreviousReviewVersionId.HasValue)
                .Select(r =>
                    r.PreviousReviewVersionId!.Value)
                .Distinct()
                .ToArray();

            var parents =
                new Dictionary<Guid, CvReviewVersion>();

            if (previousIds.Length > 0)
            {
                parents = await historyQuery
                    .Where(r => previousIds.Contains(r.Id))
                    .ToDictionaryAsync(
                        r => r.Id,
                        cancellationToken);
            }

            // Historical criteria may differ from the
            // current review criteria. Validate exact revisions.
            var revisionIds = reviews
                .Select(r => r.VacancyRevisionId)
                .Distinct()
                .ToArray();

            var revisions =
                new Dictionary<Guid, VacancyRevision>();

            var decisions =
                new List<VacancyDecision>();

            if (revisionIds.Length > 0)
            {
                revisions = await _db.VacancyRevisions
                    .AsNoTracking()
                    .Where(r =>
                        revisionIds.Contains(r.Id) &&
                        r.VacancyId ==
                            workspace.Application.VacancyId)
                    .ToDictionaryAsync(
                        r => r.Id,
                        cancellationToken);

                decisions = await _db
                    .Set<VacancyDecision>()
                    .AsNoTracking()
                    .Where(d =>
                        revisionIds.Contains(
                            d.VacancyRevisionId))
                    .ToListAsync(cancellationToken);
            }

            // Load only extraction sources referenced by
            // the requested historical review versions.
            var extractionIds = reviews
                .Where(r => r.ExtractionRunId.HasValue)
                .Select(r => r.ExtractionRunId!.Value)
                .Distinct()
                .ToArray();

            var extractions =
                new Dictionary<Guid, CvExtractionRun>();

            if (extractionIds.Length > 0)
            {
                extractions = await _db.CvExtractionRuns
                    .AsNoTracking()
                    .Where(e =>
                        extractionIds.Contains(e.Id) &&
                        e.ApplicationId == applicationId &&
                        e.DocumentId == workspace.Document.Id)
                    .ToDictionaryAsync(
                        e => e.Id,
                        cancellationToken);
            }

            var items =
                new List<CvReviewVersionResponse>(
                    reviews.Count);

            for (var index = 0;
                 index < reviews.Count;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var review = reviews[index];

                var expectedVersion =
                    totalCount - skip - index;

                if (review.Id == Guid.Empty ||
                    review.Version != expectedVersion)
                {
                    throw new InvalidOperationException(
                        "Historical review sequence is invalid.");
                }

                if (!revisions.TryGetValue(
                        review.VacancyRevisionId,
                        out var revision) ||
                    !HasExactApproval(
                        revision,
                        decisions))
                {
                    return HistoryFailure(
                        "cv_review_revision_not_approved",
                        "A historical vacancy revision is not approved.");
                }

                // Validate review metadata against its own
                // historical criteria, not the latest criteria.
                CvExtractionRun? extraction = null;

                if (review.ExtractionRunId.HasValue)
                {
                    if (!extractions.TryGetValue(
                            review.ExtractionRunId.Value,
                            out extraction) ||
                        extraction.Status is not
                            (CvExtractionStatuses.ReadyForReview
                             or CvExtractionStatuses.Failed) ||
                        !extraction.CompletedAtUtc.HasValue ||
                        extraction.CompletedAtUtc.Value >
                            review.CreatedAtUtc)
                    {
                        throw new InvalidOperationException(
                            "Historical extraction source is invalid.");
                    }
                }

                var historicalWorkspace = workspace with
                {
                    ReviewRevision = revision,
                    LatestReview = review,
                    LatestExtraction = extraction,
                    RevisionDecisions = decisions
                };

                ValidateWorkspaceSources(
                    historicalWorkspace);

                if (extraction is not null)
                {
                    // Validate stored extraction payload, but
                    // do not include it in the history response.
                    _ = MapExtraction(extraction);
                }

                if (review.Version > 1)
                {
                    if (!review.PreviousReviewVersionId.HasValue ||
                        !parents.TryGetValue(
                            review.PreviousReviewVersionId.Value,
                            out var parent) ||
                        parent.Version != review.Version - 1 ||
                        parent.CreatedAtUtc > review.CreatedAtUtc)
                    {
                        throw new InvalidOperationException(
                            "Historical review linkage is invalid.");
                    }

                    if (review.Status ==
                            CvReviewStatuses.Confirmed &&
                        (parent.Status != CvReviewStatuses.Draft ||
                         parent.VacancyRevisionId !=
                            review.VacancyRevisionId ||
                         parent.ExtractionRunId !=
                            review.ExtractionRunId ||
                         !string.Equals(
                            parent.ValuesJson,
                            review.ValuesJson,
                            StringComparison.Ordinal) ||
                         !string.Equals(
                            parent.DuplicateResolutionJson,
                            review.DuplicateResolutionJson,
                            StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException(
                            "Historical confirmation source is invalid.");
                    }
                }
                else if (review.Status ==
                         CvReviewStatuses.Confirmed)
                {
                    throw new InvalidOperationException(
                        "Confirmation requires a preceding draft.");
                }

                var mapped = MapReview(review)
                    ?? throw new InvalidOperationException(
                        "Historical review mapping failed.");

                if (review.Status ==
                    CvReviewStatuses.Confirmed)
                {
                    var resolution =
                        CvReviewJsonReader.ReadDuplicateResolution(
                            review.DuplicateResolutionJson);

                    // Historical duplicate decisions are
                    // preserved. Do not recalculate them using
                    // the current candidate database.
                    if (resolution.EmailResolution is not
                            ("NoDuplicate" or "Acknowledged") ||
                        resolution.PhoneResolution is not
                            ("NoDuplicate" or "Acknowledged") ||
                        (resolution.EmailResolution ==
                            "Acknowledged" &&
                         string.IsNullOrWhiteSpace(
                             resolution.EmailReason)) ||
                        (resolution.PhoneResolution ==
                            "Acknowledged" &&
                         string.IsNullOrWhiteSpace(
                             resolution.PhoneReason)) ||
                        (resolution.EmailResolution ==
                            "NoDuplicate" &&
                         resolution.EmailReason is not null) ||
                        (resolution.PhoneResolution ==
                            "NoDuplicate" &&
                         resolution.PhoneReason is not null) ||
                        mapped.ChangedFields.GetArrayLength() != 0)
                    {
                        throw new JsonException(
                            "Historical confirmation data is invalid.");
                    }
                }

                items.Add(mapped);
            }

            var response = new CvReviewHistoryResponse
            {
                ApplicationId =
                    workspace.Application.Id,

                DocumentId =
                    workspace.Document.Id,

                ApplicationVacancyRevisionId =
                    workspace.ApplicationRevision.Id,

                ReviewVacancyRevisionId =
                    workspace.ReviewRevision.Id,

                ApplicationRowVersion =
                    Convert.ToBase64String(
                        workspace.Application.RowVersion),

                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,

                TotalPages = (int)(
                    ((long)totalCount + query.PageSize - 1)
                    / query.PageSize),

                LatestReviewVersionId = latest?.Id,
                LatestReviewVersion = latest?.Version,
                LatestReviewStatus = latest?.Status,

                Items = items
            };

            cancellationToken.ThrowIfCancellationRequested();

            await transaction.CommitAsync(
                cancellationToken);

            return new CvReviewHistoryOperationResponse
            {
                Success = true,
                Code = "cv_review_history_retrieved",
                Message =
                    "CV review history retrieved successfully.",
                Data = response
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return HistoryFailure(
                "cv_review_json_invalid",
                "Stored CV review history is invalid.");
        }
        catch (DbException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return HistoryFailure(
                "cv_review_database_error",
                "CV review history is temporarily unavailable.");
        }
        catch (InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return HistoryFailure(
                "cv_review_source_invalid",
                "CV review history source data is invalid.");
        }
    }
}
