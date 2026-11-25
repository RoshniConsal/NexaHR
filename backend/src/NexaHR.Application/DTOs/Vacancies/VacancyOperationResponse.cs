namespace NexaHR.Application.DTOs.Vacancies;

public sealed class VacancyOperationResponse
{
    public bool Success { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public CreateVacancyResponse? Vacancy { get; set; }
}
