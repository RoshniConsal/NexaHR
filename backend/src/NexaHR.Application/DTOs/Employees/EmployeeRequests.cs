using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Employees;

public sealed class EmployeeDirectoryQuery
{
    [StringLength(200)]
    public string? Search { get; set; }

    public Guid? DepartmentId { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class UpdateEmployeeRequest
{
    public Guid? DepartmentId { get; set; }

    [StringLength(200, MinimumLength = 1)]
    public string? Designation { get; set; }

    [StringLength(50, MinimumLength = 1)]
    public string? EmploymentType { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class RequestWorkEmailVerification
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string WorkEmail { get; set; } = string.Empty;
}

public sealed class ConfirmWorkEmailVerification
{
    [Required]
    [StringLength(512)]
    public string Token { get; set; } = string.Empty;
}

public sealed class EmploymentHistoryQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
