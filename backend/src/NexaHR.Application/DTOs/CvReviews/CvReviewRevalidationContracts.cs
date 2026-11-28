using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NexaHR.Application.Validation.CvReviews;

namespace NexaHR.Application.DTOs.CvReviews;

// API-083: Explicitly start a new CV review against
// a selected approved vacancy criteria revision.
//
// The server preserves the original application revision,
// selected CV document and immutable review history.
[JsonUnmappedMemberHandling(
    JsonUnmappedMemberHandling.Disallow)]
public sealed class CvReviewRevalidationRequest
{
    [JsonRequired]
    public Guid DocumentId { get; init; }

    [JsonRequired]
    [Range(1, int.MaxValue)]
    public int DocumentVersion { get; init; }

    // Exact criteria revision currently used by the review.
    [JsonRequired]
    public Guid CurrentVacancyRevisionId { get; init; }

    // Explicitly selected target criteria revision.
    // The service must verify vacancy ownership and CEO approval.
    [JsonRequired]
    public Guid TargetVacancyRevisionId { get; init; }

    [JsonRequired]
    [Required]
    [StringLength(12, MinimumLength = 12)]
    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    // Revalidation requires an existing review version.
    [JsonRequired]
    public Guid ExpectedReviewVersionId { get; init; }

    [JsonRequired]
    [Required]
    [StringLength(12, MinimumLength = 12)]
    public string ExpectedReviewRowVersion { get; init; }
        = string.Empty;

    // Null only when no extraction run exists.
    [JsonRequired]
    public Guid? ExpectedExtractionRunId { get; init; }

    [JsonRequired]
    [StringLength(12, MinimumLength = 12)]
    public string? ExpectedExtractionRowVersion { get; init; }
}

// API-083: Successfully started re-review.
// The new review is a Draft, not a confirmation.
public sealed class CvReviewRevalidationResponse
{
    public Guid ApplicationId { get; init; }

    public Guid DocumentId { get; init; }

    // Original application submission criteria.
    public Guid ApplicationVacancyRevisionId { get; init; }

    // Criteria used before explicit revalidation.
    public Guid PreviousReviewVacancyRevisionId { get; init; }

    // Criteria used by the newly appended review.
    public Guid ReviewVacancyRevisionId { get; init; }

    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    // Revalidation does not automatically change stage.
    public string Stage { get; init; }
        = string.Empty;

    public CvReviewVersionResponse Review { get; init; }
        = new();

    public CvReviewDuplicateFlagsResponse DuplicateFlags
        { get; init; } = new()
        {
            Source = "CurrentReview"
        };

    // Indicates that a previously current confirmation
    // is no longer the current review.
    // Historical confirmation records remain immutable.
    public bool ConfirmationInvalidated { get; init; }

    // The new draft must be explicitly confirmed.
    public bool RequiresConfirmation { get; init; } = true;
}

// Internal operation result.
// Do not expose private storage or database diagnostics.
public sealed class CvReviewRevalidationOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public IReadOnlyList<CvFieldValidationError> Errors
        { get; init; } = [];

    public CvReviewRevalidationResponse? Data { get; init; }
}
