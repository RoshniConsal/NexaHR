namespace NexaHR.Application.DTOs.Vacancies;

public sealed class VacancyListItemResponse
{
    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public Guid DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string EmploymentType { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string WorkMode { get; init; } = string.Empty;

    public int Openings { get; init; }

    public DateOnly ClosingDate { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public DateTime? SubmittedAtUtc { get; init; }

    public DateTime? PublishedAtUtc { get; init; }

    public string RowVersion { get; init; } = string.Empty;
}

public sealed class VacancyListResponse
{
    public IReadOnlyList<VacancyListItemResponse> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
