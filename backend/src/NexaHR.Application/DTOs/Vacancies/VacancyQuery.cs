using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Vacancies;

public sealed class VacancyQuery
{
    [StringLength(200)]
    public string? Search { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    public Guid? DepartmentId { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
