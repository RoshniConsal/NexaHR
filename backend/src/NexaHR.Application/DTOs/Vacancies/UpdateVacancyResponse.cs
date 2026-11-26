namespace NexaHR.Application.DTOs.Vacancies;

public sealed class UpdateVacancyResponse
{
    public Guid VacancyId { get; init; }

    public Guid VacancyRevisionId { get; init; }

    public int Revision { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool NewRevisionCreated { get; init; }

    public bool PublicationPaused { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public string RowVersion { get; init; } = string.Empty;

    public string RevisionRowVersion { get; init; } = string.Empty;
}

public sealed class UpdateVacancyOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public UpdateVacancyResponse? Vacancy { get; init; }
}
