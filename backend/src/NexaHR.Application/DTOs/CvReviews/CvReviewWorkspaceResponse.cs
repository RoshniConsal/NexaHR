using System.Text.Json;

namespace NexaHR.Application.DTOs.CvReviews;

// API-079: HR-only CV review workspace.
public sealed class CvReviewWorkspaceResponse
{
    public Guid ApplicationId { get; init; }

    public Guid VacancyId { get; init; }

    public Guid ApplicationVacancyRevisionId { get; init; }

    public Guid ReviewVacancyRevisionId { get; init; }

    public string JobTitle { get; init; }
        = string.Empty;

    public string Stage { get; init; }
        = string.Empty;

    public DateTime AppliedAtUtc { get; init; }

    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    public CvReviewDocumentResponse Document { get; init; }
        = new();

    public CvReviewProfileDefaultsResponse ProfileDefaults { get; init; }
        = new();

    public CvReviewDuplicateFlagsResponse DuplicateFlags { get; set; }
        = new();

    public CvReviewExtractionResponse? LatestExtraction { get; init; }

    public CvReviewVersionResponse? LatestReview { get; init; }
}

// Metadata only. Never expose private storage paths.
public sealed class CvReviewDocumentResponse
{
    public Guid DocumentId { get; init; }

    public int Version { get; init; }

    public string OriginalFileName { get; init; }
        = string.Empty;

    public string ContentType { get; init; }
        = string.Empty;

    public long SizeBytes { get; init; }

    public string ScanStatus { get; init; }
        = string.Empty;

    public DateTime UploadedAtUtc { get; init; }

    // Null until authorized HR content access is implemented.
    public string? ContentUrl { get; init; }

    public bool CanAccessContent { get; init; }
}

// Current candidate profile defaults are not extracted CV data.
public sealed class CvReviewProfileDefaultsResponse
{
    public Guid CandidateUserId { get; init; }

    public string? FullName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Skills { get; init; }

    public string? Education { get; init; }

    public string Source { get; init; }
        = "CandidateProfile";
}

// Submission-time or freshly recalculated review flags.
// No other candidate identity is exposed.
public sealed class CvReviewDuplicateFlagsResponse
{
    public bool DuplicateEmailFlag { get; init; }

    public bool DuplicatePhoneFlag { get; init; }

    public string Source { get; init; }
        = "ApplicationSubmission";
}

// Extraction history is distinct from reviewed values.
public sealed class CvReviewExtractionResponse
{
    public Guid ExtractionRunId { get; init; }

    public string RowVersion { get; init; }
        = string.Empty;

    public Guid DocumentId { get; init; }

    public int Version { get; init; }

    public string ParserVersion { get; init; }
        = string.Empty;

    public string Status { get; init; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string? FailureCode { get; init; }

    public JsonElement? ExtractedValues { get; init; }

    public JsonElement? Evidence { get; init; }
}

// Latest immutable review version.
public sealed class CvReviewVersionResponse
{
    public Guid ReviewVersionId { get; init; }

    public Guid DocumentId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public Guid? ExtractionRunId { get; init; }

    public Guid? PreviousReviewVersionId { get; init; }

    public Guid ReviewerUserId { get; init; }

    public int Version { get; init; }

    public string Status { get; init; }
        = string.Empty;

    public JsonElement ReviewedValues { get; init; }

    public JsonElement DuplicateResolution { get; init; }

    public JsonElement ChangedFields { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ConfirmedAtUtc { get; init; }

    public string RowVersion { get; init; }
        = string.Empty;
}

// Service operation result.
public sealed class CvReviewWorkspaceOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public CvReviewWorkspaceResponse? Data { get; init; }
}
