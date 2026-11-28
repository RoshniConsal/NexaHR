using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Applications;

public sealed class SubmitApplicationRequest
{
    [Required]
    public Guid? VacancyId { get; set; }

    [Required]
    public Guid? VacancyRevisionId { get; set; }

    [Required]
    public Guid? DocumentId { get; set; }

    [Required]
    public bool? ConsentGranted { get; set; }

    [Required]
    [StringLength(100)]
    public string ConsentTextVersion { get; set; }
        = string.Empty;
}
