using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Vacancies;

public sealed class DecideVacancyRequest
{
    public Guid VacancyRevisionId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    public string RevisionRowVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Decision { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Comment { get; set; }
}
