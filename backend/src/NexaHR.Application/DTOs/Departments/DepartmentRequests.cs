using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Departments;

public sealed class DepartmentQuery
{
    [StringLength(150)]
    public string? Search { get; set; }

    public bool IncludeArchived { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class CreateDepartmentRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}

public sealed class UpdateDepartmentRequest
{
    [StringLength(150, MinimumLength = 2)]
    public string? Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool ClearDescription { get; set; }

    [Required]
    [StringLength(24)]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ArchiveDepartmentRequest
{
    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [StringLength(24)]
    public string RowVersion { get; set; } = string.Empty;
}
