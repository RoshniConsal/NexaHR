namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationListItemResponse
{
    public Guid ApplicationId { get; init; }

    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public Guid DocumentId { get; init; }

    public string JobTitle { get; init; }
        = string.Empty;

    public string DepartmentName { get; init; }
        = string.Empty;

    public string Stage { get; init; }
        = string.Empty;

    public DateTime AppliedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public Guid? CandidateUserId { get; init; }

    public string? CandidateName { get; init; }

    public string? RowVersion { get; init; }
}

public sealed class ApplicationListResponse
{
    public IReadOnlyList<ApplicationListItemResponse> Items
        { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class ApplicationListOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; }
        = string.Empty;

    public string Message { get; init; }
        = string.Empty;

    public ApplicationListResponse? Data { get; init; }
}
