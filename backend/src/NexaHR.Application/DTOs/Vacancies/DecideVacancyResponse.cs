namespace NexaHR.Application.DTOs.Vacancies;

public sealed class DecideVacancyResponse
{
    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public VacancyDecisionResponse Decision { get; init; } = new();

    public DateTime UpdatedAtUtc { get; init; }

    public string RowVersion { get; init; } = string.Empty;

    public string RevisionRowVersion { get; init; } = string.Empty;
}

public sealed class DecideVacancyOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DecideVacancyResponse? Vacancy { get; init; }
}
