using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Jobs;

public sealed class PublicJobQuery
{
    [StringLength(200)]
    public string? Search { get; set; }

    public Guid? DepartmentId { get; set; }

    [StringLength(50)]
    public string? EmploymentType { get; set; }

    [StringLength(50)]
    public string? WorkMode { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
