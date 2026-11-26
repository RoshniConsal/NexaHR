namespace NexaHR.Application.DTOs.Jobs;

public sealed class PublicJobListItemResponse
{
    public Guid JobId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string DepartmentName { get; init; } = string.Empty;

    public string EmploymentType { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string WorkMode { get; init; } = string.Empty;

    public int Openings { get; init; }

    public DateOnly ClosingDate { get; init; }

    public DateTime PublishedAtUtc { get; init; }
}

public sealed class PublicJobListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public List<PublicJobListItemResponse> Items { get; init; } = [];
}
