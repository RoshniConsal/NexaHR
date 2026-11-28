using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NexaHR.Application.Validation.CvReviews;

namespace NexaHR.Application.DTOs.CvReviews;

// API-081: Confirm the exact latest saved CV review draft.
// Confirmation does not accept replacement reviewed values,
// reviewer identity, workflow status or duplicate flags.
[JsonUnmappedMemberHandling(
    JsonUnmappedMemberHandling.Disallow)]
public sealed class CvReviewConfirmationRequest
{
    [JsonRequired]
    public Guid DocumentId { get; init; }

    [JsonRequired]
    [Range(1, int.MaxValue)]
    public int DocumentVersion { get; init; }

    [JsonRequired]
    public Guid VacancyRevisionId { get; init; }

    [JsonRequired]
    [Required]
    [StringLength(12, MinimumLength = 12)]
    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    // Confirmation always requires an existing review draft.
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

// API-081: Successfully persisted confirmation.
public sealed class CvReviewConfirmationResponse
{
    public Guid ApplicationId { get; init; }

    public Guid DocumentId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    // The application stage is not advanced automatically.
    public string Stage { get; init; }
        = string.Empty;

    // Newly appended immutable Confirmed review.
    public CvReviewVersionResponse Review { get; init; }
        = new();

    // Calculated from a fresh server-side duplicate check.
    public CvReviewDuplicateFlagsResponse DuplicateFlags
        { get; init; } = new()
        {
            Source = "CurrentReview"
        };
}

// Internal operation result.
// Never expose private storage or database diagnostics.
public sealed class CvReviewConfirmationOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public IReadOnlyList<CvFieldValidationError> Errors
        { get; init; } = [];

    public CvReviewConfirmationResponse? Data { get; init; }
}
