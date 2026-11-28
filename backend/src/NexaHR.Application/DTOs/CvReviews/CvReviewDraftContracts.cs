using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexaHR.Application.Validation.CvReviews;

namespace NexaHR.Application.DTOs.CvReviews;

// API-080: Save an immutable HR CV review draft.
// The client supplies expected source versions,
// never trusted reviewer identity or workflow status.
[JsonUnmappedMemberHandling(
    JsonUnmappedMemberHandling.Disallow)]
public sealed class CvReviewDraftRequest
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

    // Null only when no review version exists.
    [JsonRequired]
    public Guid? ExpectedReviewVersionId { get; init; }

    [JsonRequired]
    [StringLength(12, MinimumLength = 12)]
    public string? ExpectedReviewRowVersion { get; init; }

    // Null only when no extraction run exists.
    [JsonRequired]
    public Guid? ExpectedExtractionRunId { get; init; }

    // Null only when no extraction run exists.
    // Detects lifecycle changes within the same extraction run.
    [JsonRequired]
    [StringLength(12, MinimumLength = 12)]
    public string? ExpectedExtractionRowVersion { get; init; }

    // Preserve raw JSON structure until the strict
    // schema reader and semantic validators execute.
    [JsonRequired]
    public JsonElement Values { get; init; }

    [JsonRequired]
    public JsonElement DuplicateResolution { get; init; }
}

// API-080: Successful draft save result.
public sealed class CvReviewDraftResponse
{
    public Guid ApplicationId { get; init; }

    public Guid DocumentId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    // Current immutable draft and its concurrency token.
    public CvReviewVersionResponse Review { get; init; }
        = new();

    // Freshly calculated flags, not submission snapshots.
    public CvReviewDuplicateFlagsResponse DuplicateFlags
        { get; init; } = new()
        {
            Source = "CurrentReview"
        };

    // Describes whether material review values changed.
    public bool HasMaterialChanges { get; init; }

    // True only when a previously effective
    // confirmation was invalidated.
    public bool ConfirmationInvalidated { get; init; }
}

// Internal service operation result.
// Errors contain safe field paths and codes only.
public sealed class CvReviewDraftOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public IReadOnlyList<CvFieldValidationError> Errors
        { get; init; } = [];

    public CvReviewDraftResponse? Data { get; init; }
}
