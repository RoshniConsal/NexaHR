namespace NexaHR.Application.DTOs.Vacancies;

public sealed class SubmitVacancyResponse
{
    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid SubmittedByUserId { get; init; }

    public DateTime SubmittedAtUtc { get; init; }

    public string RowVersion { get; init; } = string.Empty;

    public string RevisionRowVersion { get; init; } = string.Empty;
}

public sealed class SubmitVacancyOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public SubmitVacancyResponse? Vacancy { get; init; }
}
