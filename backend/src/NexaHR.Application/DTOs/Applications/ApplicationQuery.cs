using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationQuery
{
    public Guid? VacancyId { get; set; }

    [StringLength(30)]
    public string? Stage { get; set; }

    [StringLength(200)]
    public string? Search { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
