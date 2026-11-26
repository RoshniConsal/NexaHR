using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Vacancies;

public sealed class UpdateVacancyRequest
{
    public Guid VacancyRevisionId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    public string RevisionRowVersion { get; set; } = string.Empty;

    public Guid DepartmentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string EmploymentType { get; set; } = string.Empty;

    public int Openings { get; set; }

    public string Location { get; set; } = string.Empty;

    public string WorkMode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal? MinimumExperienceYears { get; set; }

    public DateOnly ClosingDate { get; set; }

    public string HiringReason { get; set; } = string.Empty;

    [Required]
    public List<string> RequiredSkills { get; set; } = null!;

    [Required]
    public List<string> PreferredSkills { get; set; } = null!;
}
