namespace NexaHR.Application.DTOs.Vacancies;

public sealed class CreateVacancyResponse
{
    public Guid VacancyId { get; set; }

    public Guid VacancyRevisionId { get; set; }

    public int Revision { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
