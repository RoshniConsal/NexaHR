using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Vacancies;

public sealed class PublishVacancyRequest
{
    public Guid VacancyRevisionId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    public string RevisionRowVersion { get; set; } = string.Empty;
}
