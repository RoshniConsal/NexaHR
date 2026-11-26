namespace NexaHR.Application.DTOs.Vacancies;

public sealed class CloseVacancyResponse
{
    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid PublishedRevisionId { get; init; }

    public Guid PublishedByUserId { get; init; }

    public DateTime PublishedAtUtc { get; init; }

    public Guid ClosedByUserId { get; init; }

    public DateTime ClosedAtUtc { get; init; }

    public string ClosureReason { get; init; } = string.Empty;

    public string RowVersion { get; init; } = string.Empty;

    public string RevisionRowVersion { get; init; } = string.Empty;
}

public sealed class CloseVacancyOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public CloseVacancyResponse? Vacancy { get; init; }
}
