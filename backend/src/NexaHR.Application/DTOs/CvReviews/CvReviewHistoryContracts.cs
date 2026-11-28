using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.CvReviews;

// API-082: Bounded history pagination.
public sealed class CvReviewHistoryQuery
{
    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

// HR-only immutable review history.
public sealed class CvReviewHistoryResponse
{
    public Guid ApplicationId { get; init; }

    public Guid DocumentId { get; init; }

    public Guid ApplicationVacancyRevisionId { get; init; }

    public Guid ReviewVacancyRevisionId { get; init; }

    public string ApplicationRowVersion { get; init; }
        = string.Empty;

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public Guid? LatestReviewVersionId { get; init; }

    public int? LatestReviewVersion { get; init; }

    public string? LatestReviewStatus { get; init; }

    public IReadOnlyList<CvReviewVersionResponse> Items
        { get; init; } = [];
}

// Internal service operation result.
public sealed class CvReviewHistoryOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public CvReviewHistoryResponse? Data { get; init; }
}
